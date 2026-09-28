"""REST wrapper around the legacy Streamlit/Pinecone legal chatbot."""

from __future__ import annotations

import os

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


app = FastAPI(title="Legal RAG Chatbot", version="1.0.0")


class ChatRequest(BaseModel):
    question: str = Field(min_length=1, max_length=4000)
    top_k: int = Field(default=3, ge=1, le=10)


class Source(BaseModel):
    title: str = "제목 없음"
    text: str = ""
    link: str = ""


class ChatResponse(BaseModel):
    answer: str
    sources: list[Source]


def _required(name: str) -> str:
    value = os.getenv(name, "").strip()
    if not value:
        raise HTTPException(
            status_code=503,
            detail=f"{name} 환경변수가 설정되지 않았습니다.",
        )
    return value


@app.get("/health")
def health() -> dict[str, object]:
    return {
        "status": "ok",
        "configured": bool(
            os.getenv("OPENAI_API_KEY") and os.getenv("PINECONE_API_KEY")
        ),
    }


@app.post("/ask", response_model=ChatResponse)
def ask(request: ChatRequest) -> ChatResponse:
    # Optional SDKs are imported lazily so the rest of the unified server can
    # still start when the legal chatbot has not been configured yet.
    try:
        from openai import OpenAI
        from pinecone import Pinecone
    except ImportError as exc:
        raise HTTPException(
            status_code=503,
            detail="openai와 pinecone 패키지를 설치해야 합니다.",
        ) from exc

    openai_client = OpenAI(api_key=_required("OPENAI_API_KEY"))
    pinecone = Pinecone(api_key=_required("PINECONE_API_KEY"))
    index = pinecone.Index(os.getenv("PINECONE_INDEX_NAME", "law-crawlings-test"))
    namespace = os.getenv("PINECONE_NAMESPACE", "ns1")

    embedding = openai_client.embeddings.create(
        model=os.getenv("LEGAL_EMBEDDING_MODEL", "text-embedding-3-small"),
        input=request.question,
    ).data[0].embedding

    result = index.query(
        namespace=namespace,
        vector=embedding,
        top_k=request.top_k,
        include_metadata=True,
    )
    raw_matches = getattr(result, "matches", None) or []
    sources = [
        Source(
            title=str((getattr(match, "metadata", None) or {}).get("title", "제목 없음")),
            text=str((getattr(match, "metadata", None) or {}).get("text", "")),
            link=str((getattr(match, "metadata", None) or {}).get("link", "")),
        )
        for match in raw_matches
    ]
    context = "\n\n".join(
        f"제목: {source.title}\n내용: {source.text}\n링크: {source.link}"
        for source in sources
    ) or "검색 결과가 없습니다."

    completion = openai_client.chat.completions.create(
        model=os.getenv("LEGAL_CHAT_MODEL", "gpt-4o-mini"),
        messages=[
            {
                "role": "system",
                "content": (
                    "당신은 법률 정보 안내 챗봇입니다. 제공된 자료를 우선 사용하고, "
                    "확실하지 않은 내용은 단정하지 말며 전문 법률 자문이 아님을 알리세요."
                ),
            },
            {
                "role": "user",
                "content": f"참고 자료:\n{context}\n\n질문: {request.question}",
            },
        ],
        temperature=0.2,
        max_tokens=700,
    )
    return ChatResponse(
        answer=completion.choices[0].message.content or "답변을 생성하지 못했습니다.",
        sources=sources,
    )
