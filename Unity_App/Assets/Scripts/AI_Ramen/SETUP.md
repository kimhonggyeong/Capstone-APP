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

AI 서버는 모의투자 서버(8000)와 포트가 겹치지 않게 실행합니다.

```bash
uvicorn app.main:app --reload --port 8001
```

Unity는 선택된 종목코드와 종목명만 전송합니다. AI 서버가 KIS에서 현재가, 최근 종가, 외국인 수급을 가져오고 NewsAPI 키가 있으면 종목명으로 뉴스를 조회합니다.

AI 서버 `.env`에는 최소 `MONGO_URI`, `KIS_APP_KEY`, `KIS_APP_SECRET`이 필요합니다. 설명 생성에는 `OPENAI_API_KEY` 또는 실행 중인 Ollama 설정이 필요합니다.