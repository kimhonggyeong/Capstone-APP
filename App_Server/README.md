# Full Server

첨부된 주식/인증 서버와 Scenario, AI_RAMEN, market_reaction, 법률 챗봇을 한 FastAPI 프로세스와 `8000` 포트로 통합한 프로젝트입니다. 기존 소스 폴더는 수정하지 않았습니다.

## 실행

1. `.env.example`을 `.env`로 복사하고 실제 키와 DB 설정을 입력합니다.
2. PowerShell에서 이 폴더로 이동한 뒤 `./start.ps1`을 실행합니다.
3. 통합 상태는 `http://127.0.0.1:8000/health`에서 확인합니다.

직접 실행하려면 다음 명령을 사용해도 됩니다.

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe -m uvicorn main:app --host 0.0.0.0 --port 8000
```

## 서비스 주소

| 기능 | 기본 경로 | API 문서 |
|---|---|---|
| 주식·인증·주문·WebSocket | 기존 경로 유지 (`/buy`, `/portfolio`, `/ws/...`) | `/docs` |
| 시나리오 | `/scenario` | `/scenario/docs` |
| AI 판단(AI_RAMEN) | `/ai-ramen` | `/ai-ramen/docs` |
| 시장 반응 분석 | `/market-reaction` | `/market-reaction/docs` |
| 법률 RAG 챗봇 | `/legal-chatbot/ask` | `/legal-chatbot/docs` |

예를 들어 기존 AI_RAMEN의 `/judgment/005930`은 통합 서버에서 `/ai-ramen/judgment/005930`으로 호출합니다. 시나리오의 `/api/scenarios`는 `/scenario/api/scenarios`, 시장 반응의 `/simulate`는 `/market-reaction/simulate`가 됩니다.

## 주의사항

- MongoDB, KIS, OpenAI/Gemini, NewsAPI, Ollama, Pinecone 같은 외부 서비스는 해당 기능을 사용할 때만 필요합니다.
- 기존 `chatbot.py`에 하드코딩되어 있던 키는 보안상 복사하지 않았습니다. 해당 키는 폐기·재발급하고 `.env`에 새 키를 넣으세요.
- `MONGO_DB_NAME` 충돌을 피하기 위해 AI_RAMEN은 `RAMEN_MONGO_DB_NAME`, market_reaction은 `MARKET_REACTION_MONGO_DB_NAME`을 사용합니다.
