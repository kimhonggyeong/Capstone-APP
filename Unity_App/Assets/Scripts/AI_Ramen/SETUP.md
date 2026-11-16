# AI_Ramen Unity 연결

이 폴더의 C# 파일은 기존 `Real_Scripts`를 수정하지 않고 `StockCandlestickLoader`에 저장된 선택 종목을 읽어 AI 서버와 통신합니다.

1. 항상 활성 상태인 빈 GameObject에 `AiRamenController`를 추가합니다.
2. `Stock Loader`에 현재 씬의 `StockCandlestickLoader`를 연결합니다.
3. 판단근거, 히스토리, 비교 화면의 TMP 텍스트와 Content를 Inspector에 연결합니다.
4. 요인 행 프리팹에는 `AiFactorRowUI`, 히스토리 행 프리팹에는 `AiHistoryRowUI`를 추가합니다.
5. AI 패널을 여는 버튼에 기존 `StockPanelManager.Show_Real_AI_Panel`과 `AiRamenController.ShowReasoning`을 함께 연결합니다.
6. 히스토리 탭에는 기존 패널 전환 함수와 `AiRamenController.ShowHistory`를 함께 연결합니다.
7. `Compare_Buy`, `Compare_Hold`, `Compare_Sell` 오브젝트에 각각 `AiCompareView`를 붙이고 화면의 텍스트를 연결합니다.
8. Controller의 Compare Buy/Hold/Sell View에 위 세 View를 연결합니다.
9. 비교 화면의 매수/관망/매도 버튼에는 기존 패널 함수와 각각 `CompareBuy`, `CompareHold`, `CompareSell`을 연결합니다.

AI 판단은 Full_Server의 같은 8000 포트를 사용합니다. 기존 `ServerConfig.HttpBaseUrl` 설정과 Inspector 연결은 그대로 사용합니다.

```powershell
.\start.ps1
```

Unity는 선택 종목을 `/judgment/analyze`로 전송합니다. 서버는 새 ai-judgment-service의 외국인 수급, RSI, 거래량, 이동평균 교차, 최근 고저점 지표와 퍼지 계산으로 매수·매도·관망 확률을 만들고 LLM으로 설명을 작성합니다. 뉴스 분류는 새 판단 파이프라인에서 사용하지 않습니다.

기존 `/judgment/{symbol}/history`, `/judgment/compare` 주소 및 사용자별 로그인 인증을 유지합니다. 비교 신뢰도는 비교 응답의 `ai_confidence`를 우선 표시하며, 새 응답의 `probabilities`와 `ai_probabilities`도 모델에 저장합니다. 루트 비교 주소는 Unity의 기존 직접/간접 요인 사전 형식을 유지하고 `/ai-ramen/judgment/compare`는 새 서비스의 요인 목록 형식을 제공합니다.

이 앱은 기존처럼 분석 버튼 요청으로 분석합니다. 서버의 동적 감시를 사용하는 클라이언트는 `/judgment/{symbol}/watch`에 POST 하트비트를 보내고 화면을 나갈 때 DELETE를 호출합니다. 기본 TTL은 60초이며 구독이 없으면 감시 중 시세/LLM 호출도 없습니다.

서버 `.env`의 기존 `RAMEN_MONGO_DB_NAME`, `AUTH_MONGO_DB_NAME`, `JWT_SECRET`을 유지합니다. KIS 시세 조회에는 `KIS_APP_KEY`, `KIS_APP_SECRET`이 필요하며 설명 생성에는 `LLM_PROVIDER=openai`와 `OPENAI_API_KEY`, 또는 `LLM_PROVIDER=ollama`와 실행 중인 Ollama가 필요합니다. 실제 `.env`의 공급자/모델 설정은 교체 과정에서 변경하지 않습니다.
