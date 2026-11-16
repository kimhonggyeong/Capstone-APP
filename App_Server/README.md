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

기존 Unity 주소인 `/judgment/analyze`, `/judgment/{symbol}`, `/judgment/{symbol}/history`, `/judgment/compare`, `/api/scenarios`, `/simulate`도 유지합니다. 서비스별 접두사 주소는 함께 사용할 수 있습니다.

## 새 AI 판단 서비스

- `services/ramen`은 새 `ai-judgment-service`의 기술적 지표와 퍼지 계산을 사용합니다. 매수·매도·관망 확률의 합은 100%이며 LLM은 설명만 생성합니다. 뉴스 분류는 새 판단 파이프라인에 포함되지 않습니다.
- 기존 로그인 토큰, 사용자별 이력, `RAMEN_MONGO_DB_NAME` 설정을 유지합니다. 이전 이력의 confidence는 그대로 표시하고, 원래 없던 확률 값은 임의로 복원하지 않습니다.
- `/judgment/compare`는 기존 Unity의 직접/간접 요인 사전 형식, `/ai-ramen/judgment/compare`는 새 서비스의 요인 목록 형식입니다. 두 응답 모두 `ai_probabilities`와 비교 시점의 `ai_confidence`를 제공합니다.
- 기존 Unity 분석 버튼은 `/judgment/analyze`로 즉시 분석합니다. 동적 감시를 사용할 클라이언트는 `/judgment/{symbol}/watch`에 POST 하트비트를 보내고 종료 시 DELETE를 호출합니다. 기본 TTL은 60초이고, 구독자가 없으면 감시 루프는 KIS나 LLM을 호출하지 않습니다.
- `.env.example`에 새 감시/확률 계산 설정이 있습니다. 기존 실제 `.env`의 키, DB, LLM 공급자 및 모델 설정은 유지합니다. Ollama를 선택할 때는 실행 중인 Ollama와 해당 모델이 필요합니다.
- 새 원본과 동일하게 고저점 지표는 현재 KIS 한 번의 응답인 최근 최대 100거래일 기준입니다. 실제 52주 전체를 조회하는 구현은 아닙니다.

## 주의사항

- MongoDB, KIS, OpenAI/Gemini, NewsAPI, Ollama, Pinecone 같은 외부 서비스는 해당 기능을 사용할 때만 필요합니다.
- 기존 `chatbot.py`에 하드코딩되어 있던 키는 보안상 복사하지 않았습니다. 해당 키는 폐기·재발급하고 `.env`에 새 키를 넣으세요.
- `MONGO_DB_NAME` 충돌을 피하기 위해 AI_RAMEN은 `RAMEN_MONGO_DB_NAME`, market_reaction은 `MARKET_REACTION_MONGO_DB_NAME`을 사용합니다.
