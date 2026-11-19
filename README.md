# Antitude

> 실시간 주식 데이터와 모의투자, 시나리오 기반 학습, AI 분석을 결합한 Unity 금융 교육 애플리케이션

Antitude는 사용자가 실제 주식 시장과 유사한 환경에서 투자 과정을 경험하고, 금융 개념과 투자 판단을 학습할 수 있도록 만든 캡스톤 프로젝트입니다. Unity 모바일 클라이언트와 여러 기능을 하나로 통합한 FastAPI 서버로 구성되어 있습니다.

## 주요 기능

- **회원 관리**: 회원가입과 로그인, JWT 기반 인증
- **실시간 주식 정보**: 종목 검색, 현재가, 호가, 체결, 차트, 뉴스 조회
- **모의투자**: 매수·매도, 주문 정정·취소, 보유 자산과 거래 내역 관리
- **투자 시나리오**: 턴 기반 의사결정, 포트폴리오 운용, 행동 평가와 피드백
- **AI RAMEN**: 시장 요인을 바탕으로 종목 판단을 생성하고 과거 판단과 비교
- **시장 반응 분석**: 여러 분석 에이전트와 RAG를 이용한 가상 이벤트 영향 분석
- **금융 학습**: 금융 용어 사전과 퀴즈
- **법률 챗봇**: Pinecone에 저장된 법률 자료를 이용한 질의응답

## 프로젝트 구성

```text
Capston_Git/
├── App_Server/                 # FastAPI 통합 서버
│   ├── main.py                 # 모든 서비스를 8000 포트에 연결하는 진입점
│   ├── requirements.txt
│   ├── .env.example
│   ├── tests/                  # 통합 라우팅 스모크 테스트
│   └── services/
│       ├── core/               # 인증, 주식, 주문, 포트폴리오, WebSocket
│       ├── scenario/           # 시나리오 플레이 및 평가
│       ├── ramen/              # AI 종목 판단
│       ├── market_reaction/    # 시장 반응 시뮬레이션
│       └── legal_chatbot/      # 법률 RAG 챗봇
└── Unity_App/                  # Unity 클라이언트
    ├── Assets/
    │   ├── Scenes/             # 화면 씬
    │   ├── Scripts/            # 기능별 C# 코드
    │   ├── Data/               # 금융 용어 및 퀴즈 데이터
    │   └── Prefab/
    ├── Packages/
    └── ProjectSettings/
```

## 기술 스택

| 구분 | 기술 |
|---|---|
| 클라이언트 | Unity 6, C#, UGUI, TextMesh Pro, URP, NativeWebSocket |
| 서버 | Python, FastAPI, Uvicorn, Pydantic |
| 데이터베이스 | MongoDB |
| 실시간 통신 | WebSocket |
| 외부 데이터 | 한국투자증권 Open API, Naver News, NewsAPI, DART, SEC EDGAR |
| AI·RAG | OpenAI, Gemini, Ollama, Pinecone, FAISS |

## 시작하기

### 1. 준비 사항

- [Unity Hub](https://unity.com/download) 및 **Unity 6000.3.16f1**
- **Python 3.11 권장**
- 사용할 기능에 따른 MongoDB와 외부 API 계정
- Unity 클라이언트와 서버가 통신할 수 있는 네트워크 환경

모든 외부 API를 한 번에 설정할 필요는 없습니다. 예를 들어 법률 챗봇을 사용하지 않는다면 Pinecone 설정은 생략할 수 있습니다.

### 2. 서버 설정

PowerShell에서 다음 명령을 실행합니다.

```powershell
cd App_Server
Copy-Item .env.example .env
notepad .env
```

`.env`에서 사용할 기능에 필요한 값을 입력합니다.

| 기능 | 주요 환경 변수 |
|---|---|
| 인증·주식·거래 | `MONGODB_URI`, `MONGODB_DB`, `JWT_SECRET`, `KIS_APP_KEY`, `KIS_APP_SECRET` |
| 시나리오 | `DATA_BACKEND`, `MONGODB_DATABASE`, `GEMINI_API_KEY` |
| AI RAMEN | `MONGO_URI`, `OPENAI_API_KEY` 또는 Ollama 설정, `NEWSAPI_API_KEY` |
| 시장 반응 분석 | Ollama 설정, `DART_API_KEY`, MongoDB 설정 |
| 법률 챗봇 | `OPENAI_API_KEY`, `PINECONE_API_KEY`, `PINECONE_INDEX_NAME` |

> `.env`, KIS 토큰 및 승인 키 파일에는 민감한 정보가 포함됩니다. 저장소에 커밋하지 마세요.

### 3. 서버 실행

Windows에서는 제공된 실행 스크립트가 가상환경 생성, 패키지 설치, 서버 실행을 처리합니다.

```powershell
cd App_Server
.\start.ps1
```

실행 정책으로 스크립트가 차단되면 다음 파일을 실행할 수 있습니다.

```powershell
.\start.bat
```

서버가 시작되면 아래 주소에서 상태를 확인합니다.

- 상태 확인: `http://127.0.0.1:8000/health`
- 통합 API 문서: `http://127.0.0.1:8000/docs`

### 4. Unity 실행

1. Unity Hub에서 `Unity_App` 폴더를 프로젝트로 추가합니다.
2. Unity **6000.3.16f1**로 프로젝트를 엽니다.
3. `Assets/Scenes/Login.unity` 씬을 엽니다.
4. 로그인 화면의 서버 주소 설정에서 FastAPI 서버 주소를 입력합니다.
5. 에디터의 Play 버튼으로 실행합니다.

같은 PC에서 테스트하면 `http://127.0.0.1:8000`을 사용할 수 있습니다. Android 기기에서 실행할 때는 `127.0.0.1`이 아닌 **서버 PC의 내부 IP 주소**를 사용하고, Windows 방화벽에서 8000 포트 연결을 허용해야 합니다.

현재 Build Settings에 등록된 씬은 다음과 같습니다.

```text
Login → Home → Real / Scenario / Term
```

## API 안내

모든 서비스는 하나의 FastAPI 프로세스와 `8000` 포트를 사용합니다.

| 서비스 | 주요 경로 | API 문서 |
|---|---|---|
| 인증·주식·거래 | `/api/auth`, `/stocks`, `/buy`, `/sell`, `/portfolio`, `/ws/...` | `/docs` |
| 시나리오 | `/api/scenarios`, `/api/sessions`, `/scenario/...` | `/scenario/docs` |
| AI RAMEN | `/judgment/...`, `/ai-ramen/...` | `/ai-ramen/docs` |
| 시장 반응 분석 | `/simulate`, `/market-reaction/...` | `/market-reaction/docs` |
| 법률 챗봇 | `/legal-chatbot/ask` | `/legal-chatbot/docs` |

전체 엔드포인트의 요청·응답 형식은 서버 실행 후 각 Swagger 문서에서 확인할 수 있습니다.

## 테스트

서버의 통합 경로가 정상적으로 연결되는지 다음 명령으로 확인할 수 있습니다.

```powershell
cd App_Server
.\.venv\Scripts\python.exe -m pip install pytest
.\.venv\Scripts\python.exe -m pytest -q
```

## 문제 해결

### Unity에서 서버에 연결되지 않는 경우

- 서버 터미널에 `Uvicorn running on http://0.0.0.0:8000`이 표시되는지 확인합니다.
- 브라우저에서 `http://서버주소:8000/health`가 열리는지 확인합니다.
- 휴대폰과 서버 PC가 같은 네트워크에 연결되어 있는지 확인합니다.
- Unity에 저장된 서버 주소의 프로토콜(`http://` 또는 `https://`)과 포트를 확인합니다.

### 일부 AI 기능만 실패하는 경우

통합 서버가 실행되더라도 각 AI 기능에 필요한 API 키, 로컬 Ollama 모델, Pinecone 인덱스 또는 MongoDB 데이터가 없으면 해당 기능만 실패할 수 있습니다. 서버의 Swagger 문서와 터미널 오류를 함께 확인하세요.

### 실시간 주문이 동작하지 않는 경우

한국투자증권 API 키와 모의/실전 모드 설정을 확인하세요. 장 운영 시간과 휴장일에도 영향을 받습니다.

## 저장소 관리 시 권장 사항

- Unity의 `Library`, `Temp`, `Logs`, `obj`, 빌드 결과물은 커밋하지 않습니다.
- 서버의 `.env`, `.venv`, 토큰 파일은 커밋하지 않습니다.
- 기능을 추가하면 관련 API와 Unity 스크립트를 함께 문서화합니다.
- 실제 API 키가 과거에 노출된 적이 있다면 저장소에서 지우는 것만으로 끝내지 말고 반드시 폐기 후 재발급합니다.

## 라이선스

현재 저장소에는 별도의 라이선스가 명시되어 있지 않습니다. 외부 배포 또는 재사용 전에 프로젝트 소유자에게 사용 범위를 확인하세요.
