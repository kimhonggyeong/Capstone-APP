from fastapi import APIRouter, Depends, HTTPException
from services.ramen.auth import get_current_user_id
from services.ramen.history.repository import get_latest_judgment
from services.ramen.narrative.generator import generate_comparison
from services.ramen.models.schemas import (
    CompareRequest, CompareResponse, LegacyCompareResponse, empty_probabilities,
)

router = APIRouter(prefix="/judgment", tags=["judgment"])
legacy_router = APIRouter(prefix="/judgment", tags=["judgment"])


async def _comparison(payload: CompareRequest, user_id: str):
    doc = await get_latest_judgment(payload.symbol, user_id)
    if doc is None:
        raise HTTPException(status_code=404, detail="비교할 AI 판단이 없습니다")
    explanation = await generate_comparison(payload.user_judge, doc["judge"], doc["factors"])
    return doc, dict(
        user_judge=payload.user_judge, ai_judge=doc["judge"],
        ai_probabilities=doc.get("probabilities") or empty_probabilities(),
        ai_confidence=doc["confidence"],
        explanation=explanation,
    )


@router.post("/compare", response_model=CompareResponse)
async def compare_judgment(payload: CompareRequest, user_id: str = Depends(get_current_user_id)):
    doc, result = await _comparison(payload, user_id)
    return CompareResponse(**result, highlighted_factors=[f["factor"] for f in doc["factors"]])


@legacy_router.post("/compare", response_model=LegacyCompareResponse)
async def compare_judgment_legacy(payload: CompareRequest, user_id: str = Depends(get_current_user_id)):
    # 루트 주소는 기존 Unity의 직접/간접 요인 사전 형식을 보존한다.
    doc, result = await _comparison(payload, user_id)
    groups = {"직접요인": [], "간접요인": []}
    for factor in doc["factors"]:
        group = "간접요인" if factor.get("type") == "간접" else "직접요인"
        groups[group].append(factor["factor"])
    return LegacyCompareResponse(**result, highlighted_factors=groups)
