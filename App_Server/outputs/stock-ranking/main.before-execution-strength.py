from fastapi import (
    FastAPI,
    Query,
    HTTPException,
    WebSocket,
    WebSocketDisconnect,
    Depends,
    Header
)
from pydantic import BaseModel, Field, StrictInt
from typing import (
    List,
    Dict,
    Optional,
    Any,
    Literal
)
from dotenv import load_dotenv
from pathlib import Path
from threading import Lock

import os
import json
import asyncio
import io
import zipfile
import requests
import websockets
from pymongo import MongoClient, UpdateOne
from datetime import datetime, timedelta, time as dt_time

import re
import html

from urllib.parse import urlparse
from email.utils import parsedate_to_datetime

import jwt
import bcrypt

from bson import ObjectId
from pymongo.errors import DuplicateKeyError

load_dotenv()

app = FastAPI()

# =========================
# MongoDB uvicorn main_kr_mongo2:app --host 127.0.0.1 --port 8000
# =========================

MONGODB_URI = os.getenv(
    "MONGODB_URI",
    "mongodb://127.0.0.1:27017"
)

MONGODB_DB = os.getenv(
    "MONGODB_DB",
    "stock_learning_db"
)

mongo_client = MongoClient(
    MONGODB_URI,
    serverSelectionTimeoutMS=5000,
    retryWrites=True
)

mongo_db = mongo_client[MONGODB_DB]


BASE_DIR = Path(__file__).resolve().parent
DATA_DIR = BASE_DIR / "data"

KOSPI_MST_PATH = BASE_DIR / "kospi_code.mst"
KOSDAQ_MST_PATH = BASE_DIR / "kosdaq_code.mst"
MST_UPDATE_MARKER_PATH = BASE_DIR / ".mst_last_update.json"

KOSPI_MST_DOWNLOAD_URL = (
    "https://new.real.download.dws.co.kr/common/master/kospi_code.mst.zip"
)
KOSDAQ_MST_DOWNLOAD_URL = (
    "https://new.real.download.dws.co.kr/common/master/kosdaq_code.mst.zip"
)

# KIS MST 파일에서 읽어 온 KOSPI·KOSDAQ 전체 종목
stock_master: Dict[str, Dict[str, str]] = {}

users_collection = mongo_db["users"]
portfolios_collection = mongo_db["portfolios"]
trade_logs_collection = mongo_db["trade_logs"]
candle_cache_collection = mongo_db["candle_cache"]
krx_daily_collection = mongo_db["krx_daily_stocks"]
minute_candle_collection = mongo_db["minute_candle_cache"]

JWT_SECRET = os.getenv("JWT_SECRET", "")
JWT_EXPIRE_DAYS = int(
    os.getenv("JWT_EXPIRE_DAYS", "7")
)
JWT_ALGORITHM = "HS256"

if not JWT_SECRET:
    raise RuntimeError(
        "JWT_SECRET 환경변수가 필요합니다."
    )

# 사용하지 않는 캔들 캐시는 자동 삭제하여 DB가 무한히 증가하지 않도록 한다.
CANDLE_CACHE_TTL_DAYS = 30
CANDLE_CACHE_EXPIRE_DELTA = timedelta(days=CANDLE_CACHE_TTL_DAYS)

# 기존 krx_daily_stocks 컬렉션은 종목명 검색용 레거시 데이터로만 사용한다.
# 거래대금 순위는 MongoDB에 저장하지 않고 KIS 실전 API에서 즉시 조회한다.

# =========================
# KIS 설정
# =========================

KIS_APP_KEY = os.getenv("KIS_APP_KEY")
KIS_APP_SECRET = os.getenv("KIS_APP_SECRET")
KIS_MODE = os.getenv("KIS_MODE", "paper").strip().lower()

if KIS_MODE == "real":
    KIS_BASE_URL = "https://openapi.koreainvestment.com:9443"
    KIS_WS_URL = "ws://ops.koreainvestment.com:21000"
else:
    KIS_BASE_URL = "https://openapivts.koreainvestment.com:29443"
    KIS_WS_URL = "ws://ops.koreainvestment.com:31000"

# 실전/모의 토큰이 서로 섞이지 않도록 파일을 분리한다.
TOKEN_FILE = f"kis_token_{KIS_MODE}.json"
APPROVAL_FILE = f"kis_approval_{KIS_MODE}.json"

ACCESS_TOKEN: Optional[str] = None
ACCESS_TOKEN_EXPIRE_AT: Optional[datetime] = None

APPROVAL_KEY: Optional[str] = None
APPROVAL_KEY_EXPIRE_AT: Optional[datetime] = None

kis_ws_task: Optional[asyncio.Task] = None
kis_ws_symbol: Optional[str] = None
kis_ws_mode: Optional[str] = None

price_polling_tasks: Dict[str, asyncio.Task] = {}

# =========================
# 네이버 뉴스 설정
# =========================

NAVER_CLIENT_ID = os.getenv(
    "NAVER_CLIENT_ID"
)

NAVER_CLIENT_SECRET = os.getenv(
    "NAVER_CLIENT_SECRET"
)

NAVER_NEWS_URL = (
    "https://openapi.naver.com"
    "/v1/search/news.json"
)

# =========================
# 실시간 호가 WebSocket 상태
# =========================

orderbook_kis_ws_task: Optional[asyncio.Task] = None
orderbook_kis_ws_symbol: Optional[str] = None

orderbook_unity_clients: Dict[str, List[WebSocket]] = {}

PATCH_VERSION = "2026-07-23-kis-mst-stock-search-v1"


@app.get("/debug/kis-config")
def debug_kis_config():
    return {
        "patch_version": PATCH_VERSION,
        "mode": KIS_MODE,
        "base_url": KIS_BASE_URL,
        "token_file": TOKEN_FILE,
        "app_key_loaded": bool(KIS_APP_KEY),
        "app_secret_loaded": bool(KIS_APP_SECRET)
    }

# =========================
# Pydantic Models
# =========================

class CandlePoint(BaseModel):
    date: str
    open: int
    high: int
    low: int
    close: int


class CandleResponse(BaseModel):
    symbol: str
    points: List[CandlePoint]


class CandleWindowResponse(BaseModel):
    symbol: str
    interval: str
    points: List[CandlePoint]
    has_more: bool = False
    next_cursor: Optional[str] = None


class StockInfoResponse(BaseModel):
    symbol: str
    name: str
    market: str = ""

    price: int
    change: int = 0
    change_rate: float = 0.0
    change_sign: str = "0"

    open_price: int = 0
    high_price: int = 0
    low_price: int = 0
    volume: int = 0

class AuthRequest(BaseModel):
    username: str
    password: str


class AuthResponse(BaseModel):
    id: str
    username: str
    accessToken: str


class MessageResponse(BaseModel):
    message: str

class TradeRequest(BaseModel):
    symbol: str
    quantity: StrictInt = Field(gt=0, le=1_000_000)
    order_type: Literal["MARKET", "LIMIT"] = "MARKET"
    limit_price: Optional[StrictInt] = Field(default=None, gt=0, le=1_000_000_000)
    client_request_id: Optional[str] = Field(default=None, min_length=8, max_length=100)


class TradeResponse(BaseModel):
    message: str
    symbol: str
    quantity: int
    price: int
    cash: int
    order_id: str
    status: str
    filled_quantity: int = 0


class Holding(BaseModel):
    symbol: str
    name: str = ""
    quantity: int
    avg_price: float
    reservedQuantity: int = 0
    availableQuantity: int = 0


class PortfolioResponse(BaseModel):
    cash: int
    reservedCash: int = 0
    availableCash: int = 0
    holdings: List[Holding]


class StockRankItem(BaseModel):
    symbol: str
    name: str

    price: int = 0
    change_rate: float = 0.0
    volume: int = 0
    trade_value: int = 0
    market_cap: int = 0
    execution_strength: float = 0.0

    rank: int = 0


class StockRankResponse(BaseModel):
    items: List[StockRankItem]

# =========================
# 메모리 캐시
# =========================

DEFAULT_CASH = 10_000_000

def normalize_username(value: str) -> str:
    return str(value or "").strip()


def validate_credentials(username: str, password: str):
    if len(username) < 3 or len(username) > 30:
        raise HTTPException(
            status_code=400,
            detail="아이디는 3~30자로 입력해 주세요."
        )

    if len(password) < 6 or len(password.encode("utf-8")) > 72:
        raise HTTPException(
            status_code=400,
            detail="비밀번호는 6자 이상, UTF-8 기준 72바이트 이하로 입력해 주세요."
        )


def create_access_token(user_id: str) -> str:
    now = datetime.utcnow()

    payload = {
        "id": user_id,
        "iat": now,
        "exp": now + timedelta(days=JWT_EXPIRE_DAYS)
    }

    return jwt.encode(
        payload,
        JWT_SECRET,
        algorithm=JWT_ALGORITHM
    )


def get_current_user_id(
    authorization: Optional[str] = Header(default=None)
) -> str:
    if not authorization:
        raise HTTPException(
            status_code=401,
            detail="로그인이 필요합니다."
        )

    scheme, separator, token = authorization.partition(" ")

    if separator != " " or scheme.lower() != "bearer" or not token:
        raise HTTPException(
            status_code=401,
            detail="올바른 Bearer 토큰이 필요합니다."
        )

    try:
        payload = jwt.decode(
            token,
            JWT_SECRET,
            algorithms=[JWT_ALGORITHM]
        )
    except jwt.ExpiredSignatureError:
        raise HTTPException(
            status_code=401,
            detail="로그인이 만료되었습니다. 다시 로그인해 주세요."
        )
    except jwt.InvalidTokenError:
        raise HTTPException(
            status_code=401,
            detail="유효하지 않은 로그인 토큰입니다."
        )

    user_id = str(payload.get("id") or "").strip()

    if not ObjectId.is_valid(user_id):
        raise HTTPException(
            status_code=401,
            detail="로그인 사용자 정보가 올바르지 않습니다."
        )

    if users_collection.find_one({"_id": ObjectId(user_id)}) is None:
        raise HTTPException(
            status_code=401,
            detail="사용자를 찾을 수 없습니다."
        )

    return user_id

@app.post("/api/auth/signup", response_model=MessageResponse, status_code=201)
def signup(request: AuthRequest):
    username = normalize_username(request.username)
    password = request.password or ""

    validate_credentials(username, password)
    if users_collection.find_one({"username": username}):
        raise HTTPException(status_code=409, detail="이미 사용 중인 아이디입니다.")

    password_hash = bcrypt.hashpw(
        password.encode("utf-8"),
        bcrypt.gensalt(rounds=8)
    ).decode("utf-8")

    try:
        result = users_collection.insert_one({
            "username": username,
            "username_normalized": username.lower(),
            "password": password_hash,
            "created_at": datetime.utcnow(),
            "updated_at": datetime.utcnow()
        })
    except DuplicateKeyError:
        raise HTTPException(
            status_code=409,
            detail="이미 사용 중인 아이디입니다."
        )

    user_id = str(result.inserted_id)

    # Use the web-compatible shared account; no second legacy portfolio.
    app_trading.portfolio(user_id)

    return {"message": "회원가입 성공"}


@app.post("/api/auth/login", response_model=AuthResponse)
def login(request: AuthRequest):
    username = normalize_username(request.username)
    password = request.password or ""

    user = users_collection.find_one({"username": username})
    if user is None:
        user = users_collection.find_one({"username_normalized": username.lower()})

    if user is None:
        raise HTTPException(
            status_code=401,
            detail="아이디 또는 비밀번호가 올바르지 않습니다."
        )

    password_hash = str(user.get("password") or "")

    try:
        password_valid = bcrypt.checkpw(
            password.encode("utf-8"),
            password_hash.encode("utf-8")
        )
    except ValueError:
        password_valid = False

    if not password_valid:
        raise HTTPException(
            status_code=401,
            detail="아이디 또는 비밀번호가 올바르지 않습니다."
        )

    user_id = str(user["_id"])

    # Use the web-compatible shared account; no second legacy portfolio.
    app_trading.portfolio(user_id)

    return {
        "id": user_id,
        "username": user["username"],
        "accessToken": create_access_token(user_id)
    }


latest_prices: Dict[str, int] = {}
latest_previous_closes: Dict[str, int] = {}
latest_candles: Dict[str, CandlePoint] = {}
latest_names: Dict[str, str] = {}

minute_candle_locks: Dict[str, Lock] = {}

unity_clients: Dict[str, List[WebSocket]] = {}

# =========================
# 체결 목록 상태
# =========================

# 체결 WebSocket을 보고 있는 Unity 클라이언트만 관리한다.
# 체결 목록 자체는 서버에 저장하지 않는다.
execution_unity_clients: Dict[
    str,
    List[WebSocket]
] = {}

# 매수/매도 방향 추정에만 사용하는 최소 상태다.
# 체결 목록이나 과거 체결 데이터는 아니다.
latest_execution_side: Dict[str, str] = {}
latest_execution_price: Dict[str, int] = {}

# =========================
# 공통 유틸
# =========================

def parse_int(value, default: int = 0) -> int:
    try:
        if value is None:
            return default

        text = str(value).replace(",", "").strip()

        if text == "":
            return default

        return int(float(text))
    except Exception:
        return default


def normalize_symbol(symbol: str) -> str:
    symbol = symbol.strip()

    if not symbol:
        raise HTTPException(status_code=400, detail="symbol is empty")

    if len(symbol) != 6 or not symbol.isdigit():
        raise HTTPException(
            status_code=400,
            detail="Korean stock symbol must be 6 digits. Example: 005930"
        )

    return symbol


def now_time() -> dt_time:
    return datetime.now().time()


def in_time_range(start: str, end: str) -> bool:
    s = datetime.strptime(start, "%H:%M").time()
    e = datetime.strptime(end, "%H:%M").time()
    n = now_time()
    return s <= n <= e


def is_regular_market_time() -> bool:
    return in_time_range("09:00", "15:30")


def is_closing_expected_time() -> bool:
    # 장마감 예상체결가 구간
    return in_time_range("15:20", "15:30")


def is_after_close_time() -> bool:
    # 15:30~16:00 장후 시간외 종가/전환 구간
    return in_time_range("15:30", "16:00")


def is_after_hours_single_price_time() -> bool:
    # 시간외 단일가
    return in_time_range("16:00", "18:00")


def is_market_display_time() -> bool:
    return in_time_range("09:00", "18:00")


def get_realtime_tr_id() -> str:
    """
    토스/한국투자 앱 가격과 최대한 맞추기 위해
    KRX 단독 체결가가 아니라 통합 실시간체결가를 우선 사용한다.

    H0STCNT0 : 국내주식 실시간체결가 KRX
    H0STOUP0 : 국내주식 시간외 실시간체결가 KRX
    H0NXCNT0 : 국내주식 실시간체결가 NXT
    H0UNCNT0 : 국내주식 실시간체결가 통합
    """
    return "H0UNCNT0"


def extract_price_from_any(data: Any) -> int:
    """
    KIS 응답 필드명이 API마다 조금씩 다를 수 있어서
    가격 후보 필드를 넓게 잡는다.
    """
    if data is None:
        return 0

    if isinstance(data, list):
        for row in data:
            price = extract_price_from_any(row)
            if price > 0:
                return price
        return 0

    if not isinstance(data, dict):
        return parse_int(data)

    candidate_keys = [
        # 일반 현재가
        "stck_prpr",
        "STCK_PRPR",

        # 체결가 후보
        "stck_cntg_prc",
        "STCK_CNTG_PRC",
        "cntg_prc",
        "CNTG_PRC",
        "prpr",
        "PRPR",

        # 시간외 현재가 후보
        "ovtm_prpr",
        "OVTM_PRPR",
        "ovtm_untp_prpr",
        "OVTM_UNTP_PRPR",
        "ovtm_last_prc",
        "OVTM_LAST_PRC",
        "ovtm_clpr",
        "OVTM_CLPR",

        # 예상체결가 후보
        "antc_cnpr",
        "ANTC_CNPR",
        "exp_prc",
        "EXP_PRC",
        "exp_cntg_prc",
        "EXP_CNTG_PRC",
        "expc_prc",
        "EXPC_PRC",
    ]

    for key in candidate_keys:
        price = parse_int(data.get(key))
        if price > 0:
            return price

    # 마지막 방어: key 이름에 prc/prpr/cnpr가 들어간 숫자 필드 탐색
    for key, value in data.items():
        lk = str(key).lower()
        if "prc" in lk or "prpr" in lk or "cnpr" in lk or "price" in lk:
            price = parse_int(value)
            if price > 0:
                return price

    return 0


def extract_name_from_output(output: dict, symbol: str) -> str:
    return (
        output.get("hts_kor_isnm")
        or output.get("prdt_name")
        or output.get("stck_kor_isnm")
        or latest_names.get(symbol)
        or symbol
    )


def kis_get_with_retry(
    url: str,
    headers: dict,
    params: dict,
    max_retry: int = 3,
    timeout_sec: int = 8
):
    last_error = None
    last_data = None

    for attempt in range(max_retry):
        try:
            res = requests.get(
                url,
                headers=headers,
                params=params,
                timeout=timeout_sec
            )

            if res.status_code != 200:
                last_error = res.text

                if attempt < max_retry - 1:
                    wait = 1.0 + attempt
                    print(f"[KIS HTTP RETRY] {res.status_code} / wait {wait}s")
                    import time
                    time.sleep(wait)
                    continue

                raise HTTPException(status_code=502, detail=f"KIS HTTP error: {res.text}")

            data = res.json()
            last_data = data

            msg_cd = data.get("msg_cd") or data.get("message")
            msg1 = data.get("msg1", "")

            if msg_cd == "EGW00201" or "초당 거래건수" in msg1:
                if attempt < max_retry - 1:
                    wait = 1.5 + attempt
                    print(f"[KIS RATE LIMIT RETRY] {msg_cd} / wait {wait}s")
                    import time
                    time.sleep(wait)
                    continue

            return data

        except requests.exceptions.ReadTimeout as e:
            last_error = str(e)

            if attempt < max_retry - 1:
                wait = 1.5 + attempt
                print(f"[KIS READ TIMEOUT RETRY] attempt={attempt + 1}, wait={wait}s")
                import time
                time.sleep(wait)
                continue

            raise HTTPException(
                status_code=504,
                detail=f"KIS read timeout after {max_retry} retries"
            )

        except requests.exceptions.RequestException as e:
            last_error = str(e)

            if attempt < max_retry - 1:
                wait = 1.5 + attempt
                print(f"[KIS REQUEST ERROR RETRY] {e} / wait {wait}s")
                import time
                time.sleep(wait)
                continue

            raise HTTPException(
                status_code=502,
                detail=f"KIS request failed: {last_error}"
            )

    raise HTTPException(status_code=502, detail=last_data or last_error)


def load_json_file(path: str) -> Optional[dict]:
    if not os.path.exists(path):
        return None

    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        print(f"[FILE LOAD FAIL] {path}: {e}")
        return None


def save_json_file(path: str, data: dict):
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"[FILE SAVE FAIL] {path}: {e}")


# =========================
# KIS 인증
# =========================

def load_saved_access_token() -> Optional[str]:
    global ACCESS_TOKEN, ACCESS_TOKEN_EXPIRE_AT

    data = load_json_file(TOKEN_FILE)
    if not data:
        return None

    token = data.get("access_token")
    expire_at_str = data.get("expire_at")

    if not token or not expire_at_str:
        return None

    try:
        expire_at = datetime.fromisoformat(expire_at_str)
    except Exception:
        return None

    if datetime.now() >= expire_at - timedelta(minutes=10):
        print("[ACCESS TOKEN EXPIRED]")
        return None

    ACCESS_TOKEN = token
    ACCESS_TOKEN_EXPIRE_AT = expire_at

    print("[ACCESS TOKEN LOADED FROM FILE]")
    return ACCESS_TOKEN


def save_access_token(token: str, expires_in: int):
    global ACCESS_TOKEN_EXPIRE_AT

    expire_at = datetime.now() + timedelta(seconds=expires_in)
    ACCESS_TOKEN_EXPIRE_AT = expire_at

    save_json_file(TOKEN_FILE, {
        "access_token": token,
        "expire_at": expire_at.isoformat()
    })

    print("[ACCESS TOKEN SAVED]", expire_at)


def get_access_token() -> str:
    global ACCESS_TOKEN, ACCESS_TOKEN_EXPIRE_AT

    if ACCESS_TOKEN and ACCESS_TOKEN_EXPIRE_AT:
        if datetime.now() < ACCESS_TOKEN_EXPIRE_AT - timedelta(minutes=10):
            return ACCESS_TOKEN

    saved = load_saved_access_token()
    if saved:
        return saved

    url = f"{KIS_BASE_URL}/oauth2/tokenP"

    body = {
        "grant_type": "client_credentials",
        "appkey": KIS_APP_KEY,
        "appsecret": KIS_APP_SECRET
    }

    res = requests.post(url, json=body, timeout=10)

    if res.status_code != 200:
        raise HTTPException(status_code=500, detail=f"KIS token error: {res.text}")

    data = res.json()

    token = data["access_token"]
    expires_in = int(data.get("expires_in", 86400))

    ACCESS_TOKEN = token
    save_access_token(token, expires_in)

    return ACCESS_TOKEN


def load_saved_approval_key() -> Optional[str]:
    global APPROVAL_KEY, APPROVAL_KEY_EXPIRE_AT

    data = load_json_file(APPROVAL_FILE)
    if not data:
        return None

    approval_key = data.get("approval_key")
    expire_at_str = data.get("expire_at")

    if not approval_key or not expire_at_str:
        return None

    try:
        expire_at = datetime.fromisoformat(expire_at_str)
    except Exception:
        return None

    if datetime.now() >= expire_at - timedelta(minutes=10):
        print("[APPROVAL KEY EXPIRED]")
        return None

    APPROVAL_KEY = approval_key
    APPROVAL_KEY_EXPIRE_AT = expire_at

    print("[APPROVAL KEY LOADED FROM FILE]")
    return APPROVAL_KEY


def save_approval_key(approval_key: str):
    global APPROVAL_KEY_EXPIRE_AT

    expire_at = datetime.now() + timedelta(hours=23)
    APPROVAL_KEY_EXPIRE_AT = expire_at

    save_json_file(APPROVAL_FILE, {
        "approval_key": approval_key,
        "expire_at": expire_at.isoformat()
    })

    print("[APPROVAL KEY SAVED]", expire_at)


def get_approval_key() -> str:
    global APPROVAL_KEY, APPROVAL_KEY_EXPIRE_AT

    if APPROVAL_KEY and APPROVAL_KEY_EXPIRE_AT:
        if datetime.now() < APPROVAL_KEY_EXPIRE_AT - timedelta(minutes=10):
            return APPROVAL_KEY

    saved = load_saved_approval_key()
    if saved:
        return saved

    url = f"{KIS_BASE_URL}/oauth2/Approval"

    body = {
        "grant_type": "client_credentials",
        "appkey": KIS_APP_KEY,
        "secretkey": KIS_APP_SECRET
    }

    res = requests.post(url, json=body, timeout=10)

    if res.status_code != 200:
        raise HTTPException(status_code=500, detail=f"KIS approval error: {res.text}")

    data = res.json()
    APPROVAL_KEY = data["approval_key"]

    save_approval_key(APPROVAL_KEY)

    return APPROVAL_KEY


def make_kis_headers(tr_id: str, token: Optional[str] = None) -> dict:
    if token is None:
        token = get_access_token()

    return {
        "content-type": "application/json; charset=utf-8",
        "accept": "application/json",
        "authorization": f"Bearer {token}",
        "appkey": KIS_APP_KEY,
        "appsecret": KIS_APP_SECRET,
        "tr_id": tr_id,
        "custtype": "P"
    }


# =========================
# KIS REST - 가격 계열
# =========================

def get_korea_regular_price(symbol: str) -> Optional[StockInfoResponse]:
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-price"
    headers = make_kis_headers("FHKST01010100", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[REGULAR PRICE ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        return None

    output = data.get("output", {})

    price = parse_int(output.get("stck_prpr"))
    name = extract_name_from_output(output, symbol)

    change = parse_int(output.get("prdy_vrss"))
    change_rate = float(output.get("prdy_ctrt", 0) or 0)
    change_sign = str(output.get("prdy_vrss_sign", "0"))

    previous_close = price - change

    if previous_close > 0:
        latest_previous_closes[symbol] = previous_close

    if price <= 0:
        return None

    latest_prices[symbol] = price
    latest_names[symbol] = name

    print(f"[REGULAR PRICE] {symbol} {price}")

    market = str(
        stock_master.get(symbol, {}).get(
            "market",
            ""
        )
    )

    return StockInfoResponse(
        symbol=symbol,
        name=name,
        market=market,

        price=price,
        change=change,
        change_rate=change_rate,
        change_sign=change_sign,

        open_price=parse_int(
            output.get("stck_oprc")
        ),
        high_price=parse_int(
            output.get("stck_hgpr")
        ),
        low_price=parse_int(
            output.get("stck_lwpr")
        ),
        volume=parse_int(
            output.get("acml_vol")
        )
    )

def get_korea_nxt_price(
    symbol: str
) -> Optional[StockInfoResponse]:
    """
    NXT 현재가 조회.

    NXT 지원 종목:
    - 마지막 NXT 체결가 반환

    NXT 미지원 종목:
    - None 반환 후 KRX로 fallback
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = (
        f"{KIS_BASE_URL}"
        "/uapi/domestic-stock/v1/quotations/inquire-price"
    )

    headers = make_kis_headers(
        "FHKST01010100",
        token
    )

    params = {
        "FID_COND_MRKT_DIV_CODE": "NX",
        "FID_INPUT_ISCD": symbol
    }

    try:
        data = kis_get_with_retry(
            url=url,
            headers=headers,
            params=params,
            max_retry=2,
            timeout_sec=8
        )
    except Exception as error:
        print(
            f"[NXT PRICE REQUEST FAIL] "
            f"{symbol}: {error}"
        )
        return None

    if data.get("rt_cd") != "0":
        print(
            f"[NXT NOT SUPPORTED OR ERROR] "
            f"{symbol} / "
            f"{data.get('msg_cd')} / "
            f"{data.get('msg1')}"
        )
        return None

    output = data.get("output", {})

    if not isinstance(output, dict):
        return None

    price = parse_int(
        output.get("stck_prpr")
    )

    if price <= 0:
        print(
            f"[NXT PRICE EMPTY] {symbol}"
        )
        return None

    name = extract_name_from_output(
        output,
        symbol
    )

    change = parse_int(
        output.get("prdy_vrss")
    )

    change_rate = parse_float(
        output.get("prdy_ctrt")
    )

    change_sign = str(
        output.get("prdy_vrss_sign", "0")
    )

    previous_close = price - change

    if previous_close > 0:
        latest_previous_closes[symbol] = (
            previous_close
        )

    latest_prices[symbol] = price
    latest_names[symbol] = name

    print(
        f"[NXT PRICE] "
        f"{symbol} "
        f"price={price} "
        f"change={change} "
        f"rate={change_rate}"
    )

    market = str(
        stock_master.get(symbol, {}).get(
            "market",
            ""
        )
    )

    return StockInfoResponse(
        symbol=symbol,
        name=name,
        market=market,

        price=price,
        change=change,
        change_rate=change_rate,
        change_sign=change_sign,

        open_price=parse_int(
            output.get("stck_oprc")
        ),
        high_price=parse_int(
            output.get("stck_hgpr")
        ),
        low_price=parse_int(
            output.get("stck_lwpr")
        ),
        volume=parse_int(
            output.get("acml_vol")
        )
    )

def safe_get_nxt_price(
    symbol: str
) -> Optional[StockInfoResponse]:
    try:
        return get_korea_nxt_price(symbol)
    except Exception as error:
        print(
            f"[SAFE NXT PRICE FAIL] "
            f"{symbol}: {error}"
        )
        return None


def safe_get_krx_price(
    symbol: str
) -> Optional[StockInfoResponse]:
    try:
        return get_korea_regular_price(symbol)
    except Exception as error:
        print(
            f"[SAFE KRX PRICE FAIL] "
            f"{symbol}: {error}"
        )
        return None

def parse_float(value, default: float = 0.0) -> float:
    try:
        if value is None:
            return default

        text = str(value).replace(",", "").strip()

        if text == "":
            return default

        return float(text)
    except Exception:
        return default


def get_korea_orderbook_extra_info(symbol: str) -> dict:
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-price"
    headers = make_kis_headers("FHKST01010100", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[ORDERBOOK EXTRA INFO ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        return {}

    output = data.get("output", {})

    print("[ORDERBOOK EXTRA INFO RAW]", json.dumps(output, ensure_ascii=False, indent=2))

    current_price = parse_int(output.get("stck_prpr"))
    change = parse_int(output.get("prdy_vrss"))
    previous_close = current_price - change

    if previous_close > 0:
        latest_previous_closes[symbol] = previous_close

    return {
        "current_price": current_price,
        "previous_close": previous_close,

        "execution_strength": parse_float(
            output.get("tday_rltv")
            or output.get("cttr")
            or output.get("cnqn")
        ),

        "week52_high": parse_int(output.get("w52_hgpr")),
        "week52_low": parse_int(output.get("w52_lwpr")),

        "upper_limit": parse_int(output.get("stck_mxpr")),
        "lower_limit": parse_int(output.get("stck_llam")),

        "open_price": parse_int(output.get("stck_oprc")),
        "high_price": parse_int(output.get("stck_hgpr")),
        "low_price": parse_int(output.get("stck_lwpr")),

        "volume": parse_int(output.get("acml_vol")),

        "volume_vs_yesterday_rate": parse_float(
            output.get("prdy_vrss_vol_rate")
            or output.get("vol_tnrt")
        ),

        # 재무/밸류에이션 후보
        "market_cap": parse_int(
            output.get("hts_avls")
            or output.get("lstn_stcn")
            or output.get("mket_cap")
        ),
        "per": parse_float(output.get("per")),
        "pbr": parse_float(output.get("pbr")),
        "eps": parse_int(output.get("eps")),
        "bps": parse_int(output.get("bps")),
    }

def get_korea_financial_ratio(symbol: str) -> dict:
    """
    국내주식 재무비율
    KIS 문서 메뉴: [국내주식] 종목정보 > 국내주식 재무비율
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/finance/financial-ratio"
    headers = make_kis_headers("FHKST66430300", token)

    params = {
        "FID_DIV_CLS_CODE": "0",
        "fid_cond_mrkt_div_code": "J",
        "fid_input_iscd": symbol
    }

    try:
        data = kis_get_with_retry(url, headers, params, max_retry=2)

        if data.get("rt_cd") != "0":
            print("[FINANCIAL RATIO ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
            return {}

        print("[FINANCIAL RATIO RAW]", json.dumps(data, ensure_ascii=False, indent=2))

        rows = data.get("output", [])

        if isinstance(rows, dict):
            rows = [rows]

        if not rows:
            return {}

        # 보통 가장 최근 연도/분기가 첫 번째 또는 마지막에 옴.
        # 값이 있는 row 우선 선택
        target = rows[0]

        for row in rows:
            if parse_float(row.get("roe_val")) > 0 or parse_float(row.get("roe")) > 0:
                target = row
                break

        return {
            "roe": parse_float(
                target.get("roe_val")
                or target.get("roe")
                or target.get("return_on_equity")
            ),
            "pbr": parse_float(
                target.get("pbr")
                or target.get("pbr_val")
            ),
            "per": parse_float(
                target.get("per")
                or target.get("per_val")
            ),
            "eps": parse_int(
                target.get("eps")
                or target.get("eps_val")
            ),
            "bps": parse_int(
                target.get("bps")
                or target.get("bps_val")
            )
        }

    except Exception as e:
        print("[FINANCIAL RATIO FAIL]", e)
        return {}

def get_korea_income_statement(symbol: str) -> dict:
    """
    국내주식 손익계산서
    KIS 문서 메뉴: [국내주식] 종목정보 > 국내주식 손익계산서
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/finance/income-statement"
    headers = make_kis_headers("FHKST66430200", token)

    params = {
        "FID_DIV_CLS_CODE": "0",
        "fid_cond_mrkt_div_code": "J",
        "fid_input_iscd": symbol
    }

    try:
        data = kis_get_with_retry(url, headers, params, max_retry=2)

        if data.get("rt_cd") != "0":
            print("[INCOME STATEMENT ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
            return {}

        print("[INCOME STATEMENT RAW]", json.dumps(data, ensure_ascii=False, indent=2))

        rows = data.get("output", [])

        if isinstance(rows, dict):
            rows = [rows]

        if not rows:
            return {}

        target = rows[0]

        # 값 있는 row 우선
        for row in rows:
            if parse_int(row.get("sale_account")) > 0 or parse_int(row.get("sales")) > 0:
                target = row
                break

        return {
            "revenue": parse_int(
                target.get("sale_account")
                or target.get("sales")
                or target.get("revenue")
            ),
            "operating_profit": parse_int(
                target.get("bsop_prti")
                or target.get("op_prfi")
                or target.get("operating_profit")
            )
        }

    except Exception as e:
        print("[INCOME STATEMENT FAIL]", e)
        return {}

def get_korea_overtime_price(symbol: str) -> Optional[StockInfoResponse]:
    """
    국내주식 시간외현재가
    /uapi/domestic-stock/v1/quotations/inquire-overtime-price
    TR_ID: FHPST02300000
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-overtime-price"
    headers = make_kis_headers("FHPST02300000", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[OVERTIME PRICE ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        return None

    output = data.get("output", {})

    print("[OVERTIME PRICE RAW]", json.dumps(output, ensure_ascii=False, indent=2))

    price = extract_price_from_any(output)
    name = extract_name_from_output(output, symbol)

    change = parse_int(output.get("ovtm_untp_prdy_vrss"))
    change_rate = float(output.get("ovtm_untp_prdy_ctrt", 0) or 0)
    change_sign = str(output.get("ovtm_untp_prdy_vrss_sign", "0"))

    if price <= 0:
        return None

    latest_prices[symbol] = price
    latest_names[symbol] = name

    print(f"[OVERTIME PRICE] {symbol} {price}")

    return StockInfoResponse(
        symbol=symbol,
        name=name,
        price=price,
        change=change,
        change_rate=change_rate,
        change_sign=change_sign
    )


def get_korea_overtime_conclusion_price(symbol: str) -> Optional[StockInfoResponse]:
    """
    주식현재가 시간외시간별체결
    /uapi/domestic-stock/v1/quotations/inquire-time-overtimeconclusion
    TR_ID: FHPST02310000
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-time-overtimeconclusion"
    headers = make_kis_headers("FHPST02310000", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol,
        "FID_HOUR_CLS_CODE": "1"  # 1: 시간외
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[OVERTIME CONCLUSION ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        return None

    output1 = data.get("output1", {})
    output2 = data.get("output2", [])

    print("[OVERTIME CONCLUSION OUTPUT1]", json.dumps(output1, ensure_ascii=False, indent=2))
    if output2:
        print("[OVERTIME CONCLUSION FIRST]", json.dumps(output2[0], ensure_ascii=False, indent=2))

    # 최신 체결은 보통 output2 첫 번째에 들어오는 경우가 많음
    price = extract_price_from_any(output2)
    if price <= 0:
        price = extract_price_from_any(output1)

    name = extract_name_from_output(output1 if isinstance(output1, dict) else {}, symbol)

    change = parse_int(output1.get("ovtm_untp_prdy_vrss"))
    change_rate = float(output1.get("ovtm_untp_prdy_ctrt", 0) or 0)
    change_sign = str(output1.get("ovtm_untp_prdy_vrss_sign", "0"))

    if price <= 0:
        return None

    latest_prices[symbol] = price
    latest_names[symbol] = name

    print(f"[OVERTIME CONCLUSION PRICE] {symbol} {price}")

    if change == 0 and change_rate == 0.0:
        regular = get_korea_regular_price(symbol)
        if regular is not None:
            change = regular.change
            change_rate = regular.change_rate
            change_sign = regular.change_sign

    return StockInfoResponse(
        symbol=symbol,
        name=name,
        price=price,
        change=change,
        change_rate=change_rate,
        change_sign=change_sign
    )

def normalize_execution_query_time(
    before_time: Optional[str]
) -> str:
    """
    KIS 당일시간대별체결 조회 기준시간 생성.

    최초 요청:
    - 장중이면 현재시간
    - 장 시작 전이면 09:00:00
    - 장 종료 후이면 15:30:00

    추가 요청:
    - Unity가 전달한 가장 오래된 체결시간보다
      1초 이전 시간을 기준으로 조회
    """
    if before_time:
        raw = str(before_time).replace(":", "").strip()

        if len(raw) != 6 or not raw.isdigit():
            raise HTTPException(
                status_code=400,
                detail="before_time must be HHMMSS"
            )

        try:
            base_time = datetime.strptime(
                raw,
                "%H%M%S"
            )

            previous_time = (
                base_time -
                timedelta(seconds=1)
            )

            return previous_time.strftime(
                "%H%M%S"
            )

        except ValueError:
            raise HTTPException(
                status_code=400,
                detail="invalid before_time"
            )

    now = datetime.now()

    market_start = now.replace(
        hour=9,
        minute=0,
        second=0,
        microsecond=0
    )

    market_end = now.replace(
        hour=15,
        minute=30,
        second=0,
        microsecond=0
    )

    if now < market_start:
        return "090000"

    if now > market_end:
        return "153000"

    return now.strftime("%H%M%S")


def make_execution_id(
    symbol: str,
    execution_time: str,
    price: int,
    quantity: int,
    cumulative_volume: int
) -> str:
    """
    REST와 WebSocket 데이터 중복 제거용 식별값.

    서버에 저장하지 않고 응답에만 포함한다.
    """
    raw_time = str(
        execution_time or ""
    ).replace(":", "").strip()

    return (
        f"{symbol}-"
        f"{raw_time}-"
        f"{price}-"
        f"{quantity}-"
        f"{cumulative_volume}"
    )


def determine_rest_execution_side(
    symbol: str,
    price: int,
    ask_price: int,
    bid_price: int,
    previous_price: int
) -> str:
    """
    REST 체결내역의 매수·매도 방향 추정.

    매도호가 이상에서 체결:
    - 매수 체결

    매수호가 이하에서 체결:
    - 매도 체결

    호가 정보가 없으면 직전 체결가와 비교한다.
    """
    if (
        ask_price > 0 and
        price >= ask_price
    ):
        return "buy"

    if (
        bid_price > 0 and
        price <= bid_price
    ):
        return "sell"

    if previous_price > 0:
        if price > previous_price:
            return "buy"

        if price < previous_price:
            return "sell"

    return latest_execution_side.get(
        symbol,
        "neutral"
    )


def get_korea_execution_history(
    symbol: str,
    before_time: Optional[str],
    limit: int
) -> dict:
    """
    KIS 국내주식 통합 당일시간대별체결 조회.

    - KRX + NXT 통합
    - 서버 저장 없음
    - 최근 영업일 체결 조회
    - 최대 30건씩 반환
    """
    symbol = normalize_symbol(symbol)
    requested_limit = max(1, min(int(limit), 30))

    if before_time:
        raw_time = str(before_time).replace(":", "").strip()

        if len(raw_time) != 6 or not raw_time.isdigit():
            raise HTTPException(
                status_code=400,
                detail="before_time must be HHMMSS"
            )

        try:
            query_dt = datetime.strptime(
                raw_time,
                "%H%M%S"
            ) - timedelta(seconds=1)

            query_time = query_dt.strftime("%H%M%S")

        except ValueError:
            raise HTTPException(
                status_code=400,
                detail="invalid before_time"
            )
    else:
        # 통합시장은 NXT 포함 최대 20:00까지 체결되므로
        # 최신 목록 조회 시 20시를 기준으로 요청한다.
        query_time = "200000"

    token = get_access_token()

    url = (
        f"{KIS_BASE_URL}"
        "/uapi/domestic-stock/v1/quotations/"
        "inquire-time-itemconclusion"
    )

    # 중요: 기존 FHKST01010300이 아니라 공식 최신 TR_ID
    headers = make_kis_headers(
        "FHPST01060000",
        token
    )

    params = {
        # J  : KRX
        # NX : NXT
        # UN : KRX + NXT 통합
        "FID_COND_MRKT_DIV_CODE": "UN",
        "FID_INPUT_ISCD": symbol,
        "FID_INPUT_HOUR_1": query_time
    }

    print(
        "[KIS INTEGRATED EXECUTION REQUEST]",
        {
            "symbol": symbol,
            "market": "UN",
            "query_time": query_time,
            "tr_id": "FHPST01060000"
        }
    )

    data = kis_get_with_retry(
        url=url,
        headers=headers,
        params=params,
        max_retry=3,
        timeout_sec=8
    )

    print(
        "[KIS INTEGRATED EXECUTION RAW]",
        json.dumps(
            data,
            ensure_ascii=False,
            indent=2
        )
    )

    if data.get("rt_cd") != "0":
        raise HTTPException(
            status_code=502,
            detail={
                "message": "KIS 통합 체결 조회 실패",
                "msg_cd": data.get("msg_cd"),
                "msg1": data.get("msg1")
            }
        )

    rows = data.get("output2", [])

    if isinstance(rows, dict):
        rows = [rows]

    if not isinstance(rows, list):
        rows = []

    items = []

    for row in rows:
        execution_time = str(
            row.get("stck_cntg_hour")
            or ""
        ).replace(":", "").strip()

        price = parse_int(
            row.get("stck_prpr")
        )

        # 공식 응답 필드는 cnqn
        quantity = parse_int(
            row.get("cnqn")
        )

        cumulative_volume = parse_int(
            row.get("acml_vol")
        )

        ask_price = parse_int(
            row.get("askp")
        )

        bid_price = parse_int(
            row.get("bidp")
        )

        if (
            len(execution_time) < 6
            or price <= 0
            or quantity <= 0
        ):
            continue

        execution_time = execution_time[:6]

        side = determine_execution_side(
            symbol=symbol,
            price=price,
            ask_price=ask_price,
            bid_price=bid_price
        )

        items.append({
            "id": (
                f"{symbol}-"
                f"{execution_time}-"
                f"{price}-"
                f"{quantity}-"
                f"{cumulative_volume}"
            ),
            "symbol": symbol,
            "time": format_execution_time(
                execution_time
            ),
            "price": price,
            "side": side,
            "quantity": quantity,
            "cumulative_volume": cumulative_volume
        })

    selected = items[:requested_limit]

    next_cursor = None

    if selected:
        next_cursor = str(
            selected[-1].get("time", "")
        ).replace(":", "")

    has_more = (
        len(selected) >= requested_limit
        and next_cursor is not None
        and next_cursor > "080000"
    )

    return {
        "symbol": symbol,
        "has_more": has_more,
        "next_cursor": next_cursor,
        "items": selected
    }

def get_korea_overtime_asking_price(symbol: str) -> Optional[StockInfoResponse]:
    """
    국내주식 시간외호가
    /uapi/domestic-stock/v1/quotations/inquire-overtime-asking-price
    TR_ID: FHPST02300400
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-overtime-asking-price"
    headers = make_kis_headers("FHPST02300400", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[OVERTIME ASKING ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        return None

    output = data.get("output", {})

    print("[OVERTIME ASKING RAW]", json.dumps(output, ensure_ascii=False, indent=2))

    price = extract_price_from_any(output)
    name = latest_names.get(symbol, symbol)

    if price <= 0:
        return None

    latest_prices[symbol] = price
    latest_names[symbol] = name

    print(f"[OVERTIME ASKING PRICE] {symbol} {price}")

    return StockInfoResponse(symbol=symbol, name=name, price=price)


def get_korea_exp_closing_price(symbol: str) -> Optional[StockInfoResponse]:
    """
    국내주식 장마감 예상체결가
    /uapi/domestic-stock/v1/quotations/exp-closing-price
    TR_ID: FHKST117300C0

    주의:
    이 API는 개별 종목 직접 조회라기보다 화면/시장 분류 기반 랭킹형에 가까워서,
    symbol이 output에 있으면 그 row를 찾아서 사용한다.
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/exp-closing-price"
    headers = make_kis_headers("FHKST117300C0", token)

    # 0001: 거래소, 1001: 코스닥. 삼성전자 같은 코스피는 0001.
    # 특정 종목만 바로 안 나올 수 있어서 output에서 symbol row를 찾는 방식.
    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": "0001",
        "FID_RANK_SORT_CLS_CODE": "0",
        "FID_COND_SCR_DIV_CODE": "11173",
        "FID_BLNG_CLS_CODE": "0"
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[EXP CLOSING ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        return None

    rows = data.get("output", [])

    if isinstance(rows, dict):
        rows = [rows]

    target_row = None

    for row in rows:
        code = (
            row.get("stck_shrn_iscd")
            or row.get("mksc_shrn_iscd")
            or row.get("iscd")
            or row.get("pdno")
            or ""
        )

        if str(code).strip() == symbol:
            target_row = row
            break

    if target_row is None and rows:
        # 디버그용: 삼성전자 row를 못 찾으면 첫 row 출력만 하고 fallback
        print("[EXP CLOSING FIRST ROW]", json.dumps(rows[0], ensure_ascii=False, indent=2))
        return None

    print("[EXP CLOSING RAW]", json.dumps(target_row, ensure_ascii=False, indent=2))

    price = extract_price_from_any(target_row)
    name = extract_name_from_output(target_row, symbol)

    if price <= 0:
        return None

    latest_prices[symbol] = price
    latest_names[symbol] = name

    print(f"[EXP CLOSING PRICE] {symbol} {price}")

    return StockInfoResponse(symbol=symbol, name=name, price=price)


def get_korea_best_price(
    symbol: str
) -> StockInfoResponse:
    """
    Unity에 표시할 대표 현재가.

    정책:
    1. NXT 지원 종목
       → NXT 최종 체결가 우선
    2. NXT 미지원 종목
       → KRX 현재가/종가
    3. 장중 실시간 변경
       → H0UNCNT0 통합 WebSocket이 갱신

    주말에도 NXT API에 마지막 체결가가 남아 있으면
    NXT 최종 체결가가 표시된다.
    """
    symbol = normalize_symbol(symbol)

    # 1순위: NXT
    nxt_info = safe_get_nxt_price(symbol)

    if (
        nxt_info is not None
        and nxt_info.price > 0
    ):
        print(
            f"[BEST PRICE SOURCE] "
            f"{symbol} NXT "
            f"{nxt_info.price}"
        )

        return nxt_info

    # 2순위: KRX
    krx_info = safe_get_krx_price(symbol)

    if (
        krx_info is not None
        and krx_info.price > 0
    ):
        print(
            f"[BEST PRICE SOURCE] "
            f"{symbol} KRX "
            f"{krx_info.price}"
        )

        return krx_info

    raise HTTPException(
        status_code=404,
        detail="KRX/NXT price not found"
    )


# =========================
# KIS REST - 캔들
# =========================

def get_period_code(interval: str) -> str:
    if interval == "1wk":
        return "W"
    if interval == "1mo":
        return "M"
    if interval == "1y":
        return "Y"
    return "D"


def get_start_date_by_period(period: str) -> str:
    today = datetime.now()

    if period == "1d":
        start = today - timedelta(days=1)
    elif period == "5d":
        start = today - timedelta(days=7)
    elif period == "1w":
        start = today - timedelta(days=7)
    elif period == "1mo":
        start = today - timedelta(days=40)
    elif period == "3mo":
        start = today - timedelta(days=100)
    elif period == "6mo":
        start = today - timedelta(days=200)
    elif period == "1y":
        start = today - timedelta(days=370)
    elif period == "5y":
        start = today - timedelta(days=365 * 5 + 30)
    elif period == "all":
        # 전체 차트용. KIS가 한 번에 100건만 주므로 아래에서 여러 번 나눠 호출함.
        start = today - timedelta(days=365 * 20)
    else:
        start = today - timedelta(days=365 * 20)

    return start.strftime("%Y%m%d")


def parse_kis_date(date_text: str) -> Optional[datetime]:
    try:
        if not date_text or len(date_text) != 8:
            return None
        return datetime.strptime(date_text, "%Y%m%d")
    except Exception:
        return None


def make_daily_candle_from_row(row: dict, interval: str = "1d") -> Optional[CandlePoint]:
    date_raw = row.get("stck_bsop_date") or ""

    if len(date_raw) == 8:
        year = date_raw[0:4]
        month = date_raw[4:6]
        day = date_raw[6:8]

        if interval == "1y":
            label = year          # 연봉: 2026
        elif interval == "1mo":
            label = f"{year}-{month}"  # 월봉: 2026-05
        elif interval == "1wk":
            label = f"{month}-{day}"   # 주봉: 05-08
        else:
            label = f"{month}-{day}"   # 일봉: 05-08
    else:
        label = date_raw

    open_price = parse_int(row.get("stck_oprc"))
    high_price = parse_int(row.get("stck_hgpr"))
    low_price = parse_int(row.get("stck_lwpr"))
    close_price = parse_int(row.get("stck_clpr"))

    if open_price <= 0 or high_price <= 0 or low_price <= 0 or close_price <= 0:
        return None

    return CandlePoint(
        date=label,
        open=open_price,
        high=high_price,
        low=low_price,
        close=close_price
    )


def fetch_korea_period_candle_page(
    symbol: str,
    start_date: str,
    end_date: str,
    interval: str
) -> List[dict]:
    """
    국내주식기간별시세(일/주/월/년)
    /uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice
    TR_ID: FHKST03010100

    KIS 문서 기준 한 번 호출에 최대 100건까지 반환되므로,
    전체 차트는 이 함수를 여러 번 호출해서 이어붙인다.
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice"
    headers = make_kis_headers("FHKST03010100", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol,
        "FID_INPUT_DATE_1": start_date,
        "FID_INPUT_DATE_2": end_date,
        "FID_PERIOD_DIV_CODE": get_period_code(interval),
        "FID_ORG_ADJ_PRC": "0"
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        print("[PERIOD CANDLE API ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        raise HTTPException(status_code=500, detail=data)

    rows = data.get("output2", [])

    if not isinstance(rows, list):
        return []

    return rows


def make_candle_doc_from_row(symbol: str, interval: str, row: dict) -> Optional[dict]:
    date_key = row.get("stck_bsop_date") or ""

    if not date_key:
        return None

    candle = make_daily_candle_from_row(row, interval)

    if candle is None:
        return None

    return {
        "symbol": symbol,
        "interval": interval,
        "date_key": date_key,          # 정렬/증분 조회용 원본 날짜: 20260508
        "date": candle.date,           # Unity 표시용 날짜: 05-08 / 2026-05 / 2026
        "open": candle.open,
        "high": candle.high,
        "low": candle.low,
        "close": candle.close,
        "updated_at": datetime.now(),
        "expires_at": datetime.now() + CANDLE_CACHE_EXPIRE_DELTA
    }


def candle_doc_to_point(doc: dict) -> CandlePoint:
    return CandlePoint(
        date=doc.get("date", ""),
        open=parse_int(doc.get("open")),
        high=parse_int(doc.get("high")),
        low=parse_int(doc.get("low")),
        close=parse_int(doc.get("close"))
    )


def save_candle_rows_to_mongo(symbol: str, interval: str, rows: List[dict]) -> int:
    operations = []

    for row in rows:
        doc = make_candle_doc_from_row(symbol, interval, row)

        if doc is None:
            continue

        operations.append(
            UpdateOne(
                {
                    "symbol": symbol,
                    "interval": interval,
                    "date_key": doc["date_key"]
                },
                {"$set": doc},
                upsert=True
            )
        )

    if not operations:
        return 0

    result = candle_cache_collection.bulk_write(operations, ordered=False)

    return result.upserted_count + result.modified_count


def get_cached_candle_docs(
    symbol: str,
    interval: str,
    start_date: str
) -> List[dict]:
    return list(
        candle_cache_collection
        .find({
            "symbol": symbol,
            "interval": interval,
            "date_key": {"$gte": start_date}
        })
        .sort("date_key", 1)
    )


def get_first_cached_candle_doc(symbol: str, interval: str) -> Optional[dict]:
    return candle_cache_collection.find_one(
        {
            "symbol": symbol,
            "interval": interval
        },
        sort=[("date_key", 1)]
    )


def get_last_cached_candle_doc(symbol: str, interval: str) -> Optional[dict]:
    return candle_cache_collection.find_one(
        {
            "symbol": symbol,
            "interval": interval
        },
        sort=[("date_key", -1)]
    )


def fetch_and_store_daily_candle_range(
    symbol: str,
    interval: str,
    start_date: str,
    end_date: str
) -> int:
    """
    start_date ~ end_date 구간을 KIS에서 받아 MongoDB에 저장.
    날짜 형식: YYYYMMDD
    """
    symbol = normalize_symbol(symbol)

    start_dt = datetime.strptime(start_date, "%Y%m%d")
    end_dt = datetime.strptime(end_date, "%Y%m%d")

    if start_dt > end_dt:
        return 0

    current_end_dt = end_dt
    saved_count = 0
    max_pages = 80

    for page in range(max_pages):
        if current_end_dt < start_dt:
            break

        request_start = start_dt.strftime("%Y%m%d")
        request_end = current_end_dt.strftime("%Y%m%d")

        print(f"[CACHE FETCH REQUEST] {symbol} {interval} {request_start} ~ {request_end}")

        rows = fetch_korea_period_candle_page(
            symbol=symbol,
            start_date=request_start,
            end_date=request_end,
            interval=interval
        )

        if not rows:
            break

        saved = save_candle_rows_to_mongo(symbol, interval, rows)
        saved_count += saved

        oldest_dt = None

        for row in rows:
            date_raw = row.get("stck_bsop_date") or ""
            row_dt = parse_kis_date(date_raw)

            if row_dt is not None:
                if oldest_dt is None or row_dt < oldest_dt:
                    oldest_dt = row_dt

        print(
            f"[CACHE FETCH RESULT] {symbol} {interval} "
            f"page={page + 1}, rows={len(rows)}, saved={saved}"
        )

        if oldest_dt is None:
            break

        if oldest_dt <= start_dt:
            break

        current_end_dt = oldest_dt - timedelta(days=1)

        import time
        time.sleep(0.15)

    return saved_count



def get_cached_daily_candle_window_docs(
    symbol: str,
    interval: str,
    before: Optional[str],
    limit: int
) -> List[dict]:
    query = {
        "symbol": symbol,
        "interval": interval
    }

    if before:
        query["date_key"] = {"$lt": before}

    return list(
        candle_cache_collection
        .find(query)
        .sort("date_key", -1)
        .limit(limit)
    )


def is_daily_window_cache_fresh(
    symbol: str,
    interval: str,
    max_age_minutes: int = 10
) -> bool:
    last_doc = get_last_cached_candle_doc(symbol, interval)

    if last_doc is None:
        return False

    updated_at = last_doc.get("updated_at")

    if not isinstance(updated_at, datetime):
        return False

    return datetime.now() - updated_at <= timedelta(minutes=max_age_minutes)


def fetch_daily_candle_window_into_cache(
    symbol: str,
    interval: str,
    before: Optional[str],
    required_count: int
):
    """
    전체 기간을 저장하지 않고 요청한 커서 이전 구간만 필요한 개수만큼 채운다.
    KIS 한 페이지가 최대 100건이므로 부족한 만큼만 뒤로 이동하며 호출한다.
    """
    symbol = normalize_symbol(symbol)
    target_count = max(1, required_count)
    earliest_supported_date = "19800101"

    # 최신 구간 요청은 DB에 충분한 데이터가 있어도 오래되었으면 한 페이지만 갱신한다.
    if before is None and not is_daily_window_cache_fresh(symbol, interval):
        today_date = datetime.now().strftime("%Y%m%d")
        rows = fetch_korea_period_candle_page(
            symbol=symbol,
            start_date=earliest_supported_date,
            end_date=today_date,
            interval=interval
        )
        save_candle_rows_to_mongo(symbol, interval, rows)

    docs = get_cached_daily_candle_window_docs(
        symbol=symbol,
        interval=interval,
        before=before,
        limit=target_count
    )

    if len(docs) >= target_count:
        return

    if docs:
        oldest_key = docs[-1].get("date_key")
        oldest_dt = parse_kis_date(oldest_key)
        current_end_dt = oldest_dt - timedelta(days=1) if oldest_dt else datetime.now()
    elif before:
        before_dt = parse_kis_date(before)
        current_end_dt = before_dt - timedelta(days=1) if before_dt else datetime.now()
    else:
        current_end_dt = datetime.now()

    max_pages = max(2, ((target_count - len(docs)) + 99) // 100 + 1)
    previous_oldest = None

    for page in range(max_pages):
        if len(docs) >= target_count:
            break

        request_end = current_end_dt.strftime("%Y%m%d")

        if request_end < earliest_supported_date:
            break

        print(
            f"[LAZY DAILY FETCH] {symbol} {interval} "
            f"page={page + 1}, end={request_end}"
        )

        rows = fetch_korea_period_candle_page(
            symbol=symbol,
            start_date=earliest_supported_date,
            end_date=request_end,
            interval=interval
        )

        if not rows:
            break

        save_candle_rows_to_mongo(symbol, interval, rows)

        oldest_dt = None

        for row in rows:
            row_dt = parse_kis_date(row.get("stck_bsop_date") or "")

            if row_dt is not None and (oldest_dt is None or row_dt < oldest_dt):
                oldest_dt = row_dt

        if oldest_dt is None:
            break

        oldest_key = oldest_dt.strftime("%Y%m%d")

        if previous_oldest == oldest_key:
            break

        previous_oldest = oldest_key
        current_end_dt = oldest_dt - timedelta(days=1)

        docs = get_cached_daily_candle_window_docs(
            symbol=symbol,
            interval=interval,
            before=before,
            limit=target_count
        )

        import time
        time.sleep(0.15)


def get_korea_daily_candle_window(
    symbol: str,
    interval: str,
    before: Optional[str],
    limit: int
) -> tuple[List[CandlePoint], bool, Optional[str]]:
    symbol = normalize_symbol(symbol)
    fetch_count = limit + 1

    fetch_daily_candle_window_into_cache(
        symbol=symbol,
        interval=interval,
        before=before,
        required_count=fetch_count
    )

    docs = get_cached_daily_candle_window_docs(
        symbol=symbol,
        interval=interval,
        before=before,
        limit=fetch_count
    )

    has_more = len(docs) > limit
    selected_docs = docs[:limit]
    selected_docs.reverse()

    points = [
        candle_doc_to_point(doc)
        for doc in selected_docs
        if parse_int(doc.get("open")) > 0
        and parse_int(doc.get("high")) > 0
        and parse_int(doc.get("low")) > 0
        and parse_int(doc.get("close")) > 0
    ]

    next_cursor = selected_docs[0].get("date_key") if selected_docs else None

    return points, has_more, next_cursor


def get_korea_daily_candles_cached(
    symbol: str,
    period: str = "all",
    interval: str = "1d"
) -> List[CandlePoint]:
    """
    일봉/주봉/월봉/년봉 MongoDB 캐시 기반 조회.

    동작:
    1. MongoDB에 데이터가 없으면 전체 다운로드 후 저장
    2. 데이터가 있으면 마지막 날짜 이후만 KIS에서 추가 다운로드
    3. 더 긴 period가 요청되면 앞쪽 과거 데이터도 추가 다운로드
    4. 최종 반환은 MongoDB 기준
    """
    symbol = normalize_symbol(symbol)

    final_start_date = get_start_date_by_period(period)
    today_date = datetime.now().strftime("%Y%m%d")

    first_doc = get_first_cached_candle_doc(symbol, interval)
    last_doc = get_last_cached_candle_doc(symbol, interval)

    # 1. 캐시가 아예 없는 경우
    if first_doc is None or last_doc is None:
        print(f"[CACHE MISS] {symbol} {interval} full download")
        fetch_and_store_daily_candle_range(
            symbol=symbol,
            interval=interval,
            start_date=final_start_date,
            end_date=today_date
        )

    else:
        first_date = first_doc.get("date_key")
        last_date = last_doc.get("date_key")

        # 2. 현재 요청 시작일보다 캐시 시작일이 늦으면, 과거 데이터 추가 다운로드
        if first_date and first_date > final_start_date:
            previous_end_dt = datetime.strptime(first_date, "%Y%m%d") - timedelta(days=1)
            previous_end_date = previous_end_dt.strftime("%Y%m%d")

            print(
                f"[CACHE NEED PAST] {symbol} {interval} "
                f"{final_start_date} ~ {previous_end_date}"
            )

            fetch_and_store_daily_candle_range(
                symbol=symbol,
                interval=interval,
                start_date=final_start_date,
                end_date=previous_end_date
            )

        # 3. 마지막 저장 날짜 이후 데이터 추가 다운로드
        if last_date and last_date < today_date:
            next_start_dt = datetime.strptime(last_date, "%Y%m%d") + timedelta(days=1)
            next_start_date = next_start_dt.strftime("%Y%m%d")

            print(
                f"[CACHE NEED RECENT] {symbol} {interval} "
                f"{next_start_date} ~ {today_date}"
            )

            fetch_and_store_daily_candle_range(
                symbol=symbol,
                interval=interval,
                start_date=next_start_date,
                end_date=today_date
            )

        else:
            print(f"[CACHE HIT] {symbol} {interval}")

    docs = get_cached_candle_docs(
        symbol=symbol,
        interval=interval,
        start_date=final_start_date
    )

    points = []

    for doc in docs:
        point = candle_doc_to_point(doc)

        if point.open > 0 and point.high > 0 and point.low > 0 and point.close > 0:
            points.append(point)

    print(f"[CACHE RETURN] {symbol} {interval} {len(points)} candles")

    return points


# =========================
# KIS REST - 당일 전체 분봉
# =========================

def decrease_hhmmss(time_text: str, minutes: int = 30) -> str:
    """
    HHMMSS 문자열에서 minutes만큼 빼기.
    """
    try:
        dt = datetime.strptime(time_text, "%H%M%S")
        dt = dt - timedelta(minutes=minutes)
        return dt.strftime("%H%M%S")
    except Exception:
        return "090000"


def normalize_market_minute_end_time() -> str:
    """
    당일 분봉 조회의 끝 시간을 만든다.
    장중이면 현재 시각, 장 이후면 15:30 기준.
    """
    now = datetime.now()

    market_start = now.replace(hour=9, minute=0, second=0, microsecond=0)
    market_end = now.replace(hour=15, minute=30, second=0, microsecond=0)

    if now < market_start:
        return "090000"

    if now > market_end:
        return "153000"

    return now.strftime("%H%M%S")


def fetch_korea_minute_page(symbol: str, end_time: str) -> List[dict]:
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-time-itemchartprice"
    headers = make_kis_headers("FHKST03010200", token)

    params = {
        "FID_ETC_CLS_CODE": "",
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol,
        "FID_INPUT_HOUR_1": end_time,
        "FID_PW_DATA_INCU_YN": "Y"
    }

    data = kis_get_with_retry(
        url,
        headers,
        params,
        max_retry=2,
        timeout_sec=8
    )

    if data.get("rt_cd") != "0":
        print("[MINUTE PAGE API ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
        raise HTTPException(status_code=502, detail=data)

    rows = data.get("output2", [])

    if not isinstance(rows, list):
        return []

    return rows


def make_minute_candle_from_row(row: dict) -> Optional[CandlePoint]:
    time_raw = row.get("stck_cntg_hour") or ""

    if len(time_raw) >= 4:
        label = f"{time_raw[0:2]}:{time_raw[2:4]}"
    else:
        label = time_raw

    open_price = parse_int(row.get("stck_oprc"))
    high_price = parse_int(row.get("stck_hgpr"))
    low_price = parse_int(row.get("stck_lwpr"))

    close_price = parse_int(
        row.get("stck_prpr")
        or row.get("stck_clpr")
        or row.get("stck_oprc")
    )

    if open_price <= 0 or high_price <= 0 or low_price <= 0 or close_price <= 0:
        return None

    return CandlePoint(
        date=label,
        open=open_price,
        high=high_price,
        low=low_price,
        close=close_price
    )

def get_today_key() -> str:
    return datetime.now().strftime("%Y%m%d")


def delete_old_minute_candles(symbol: str):
    today_key = get_today_key()

    result = minute_candle_collection.delete_many({
        "symbol": symbol,
        "date_key": {"$ne": today_key}
    })

    if result.deleted_count > 0:
        print(f"[MINUTE OLD DATA DELETED] {symbol} count={result.deleted_count}")


def get_last_minute_candle_doc(symbol: str) -> Optional[dict]:
    today_key = get_today_key()

    return minute_candle_collection.find_one(
        {
            "symbol": symbol,
            "date_key": today_key
        },
        sort=[("time_key", -1)]
    )


def make_minute_candle_doc(symbol: str, row: dict) -> Optional[dict]:
    time_raw = row.get("stck_cntg_hour") or ""

    if len(time_raw) < 4:
        return None

    label = f"{time_raw[0:2]}:{time_raw[2:4]}"

    open_price = parse_int(row.get("stck_oprc"))
    high_price = parse_int(row.get("stck_hgpr"))
    low_price = parse_int(row.get("stck_lwpr"))

    close_price = parse_int(
        row.get("stck_prpr")
        or row.get("stck_clpr")
        or row.get("stck_oprc")
    )

    if open_price <= 0 or high_price <= 0 or low_price <= 0 or close_price <= 0:
        return None

    return {
        "symbol": symbol,
        "date_key": get_today_key(),
        "time_key": time_raw,
        "date": label,
        "open": open_price,
        "high": high_price,
        "low": low_price,
        "close": close_price,
        "updated_at": datetime.now(),
        "expires_at": datetime.now() + CANDLE_CACHE_EXPIRE_DELTA
    }


def save_minute_rows_to_mongo(symbol: str, rows: List[dict]) -> int:
    operations = []

    for row in rows:
        doc = make_minute_candle_doc(symbol, row)

        if doc is None:
            continue

        operations.append(
            UpdateOne(
                {
                    "symbol": symbol,
                    "date_key": doc["date_key"],
                    "time_key": doc["time_key"]
                },
                {"$set": doc},
                upsert=True
            )
        )

    if not operations:
        return 0

    result = minute_candle_collection.bulk_write(operations, ordered=False)

    return result.upserted_count + result.modified_count


def get_cached_minute_candles(symbol: str) -> List[CandlePoint]:
    today_key = get_today_key()

    docs = list(
        minute_candle_collection
        .find({
            "symbol": symbol,
            "date_key": today_key
        })
        .sort("time_key", 1)
    )

    points: List[CandlePoint] = []

    for doc in docs:
        point = CandlePoint(
            date=doc.get("date", ""),
            open=parse_int(doc.get("open")),
            high=parse_int(doc.get("high")),
            low=parse_int(doc.get("low")),
            close=parse_int(doc.get("close"))
        )

        if point.open > 0 and point.high > 0 and point.low > 0 and point.close > 0:
            points.append(point)

    return points


def update_today_minute_candles(symbol: str):
    symbol = normalize_symbol(symbol)

    delete_old_minute_candles(symbol)

    last_doc = get_last_minute_candle_doc(symbol)
    last_time_key = last_doc.get("time_key") if last_doc else None

    end_time = normalize_market_minute_end_time()
    min_time = "090000"

    saved_count = 0
    max_pages = 20

    for page in range(max_pages):
        print(f"[MINUTE DB UPDATE REQUEST] {symbol} end_time={end_time}")

        rows = fetch_korea_minute_page(symbol, end_time)

        if not rows:
            break

        new_rows = []
        oldest_time = None
        reached_cached_area = False

        for row in rows:
            time_raw = row.get("stck_cntg_hour") or ""

            if not time_raw:
                continue

            if oldest_time is None or time_raw < oldest_time:
                oldest_time = time_raw

            # 이미 저장한 마지막 시간 이하로 내려오면 더 이상 과거로 갈 필요 없음
            if last_time_key is not None and time_raw <= last_time_key:
                reached_cached_area = True
                continue

            new_rows.append(row)

        saved = save_minute_rows_to_mongo(symbol, new_rows)
        saved_count += saved

        print(
            f"[MINUTE DB UPDATE RESULT] {symbol} "
            f"page={page + 1}, new_rows={len(new_rows)}, saved={saved}"
        )

        if reached_cached_area:
            break

        if oldest_time is None:
            break

        if oldest_time <= min_time:
            break

        end_time = decrease_hhmmss(oldest_time, minutes=1)

        import time
        time.sleep(0.15)

    print(f"[MINUTE DB UPDATE DONE] {symbol} saved={saved_count}")

def refresh_today_minute_candles_full(symbol: str):
    """
    오늘 1분봉을 09:00부터 현재/장마감 시각까지 최대한 전체 재동기화한다.
    기존 오늘 분봉을 지우고 다시 저장해서 중간 빈 구간을 없앤다.
    """
    symbol = normalize_symbol(symbol)

    today_key = get_today_key()

    delete_old_minute_candles(symbol)

    # 오늘 데이터도 일단 지움. 중간에 비어 있는 캐시 때문에 차트 튀는 것 방지.
    deleted = minute_candle_collection.delete_many({
        "symbol": symbol,
        "date_key": today_key
    })

    if deleted.deleted_count > 0:
        print(f"[MINUTE TODAY DATA RESET] {symbol} count={deleted.deleted_count}")

    end_time = normalize_market_minute_end_time()
    min_time = "090000"

    saved_count = 0
    max_pages = 20

    for page in range(max_pages):
        print(f"[MINUTE FULL REQUEST] {symbol} end_time={end_time}")

        rows = fetch_korea_minute_page(symbol, end_time)

        if not rows:
            break

        saved = save_minute_rows_to_mongo(symbol, rows)
        saved_count += saved

        oldest_time = None

        for row in rows:
            time_raw = row.get("stck_cntg_hour") or ""

            if not time_raw:
                continue

            if oldest_time is None or time_raw < oldest_time:
                oldest_time = time_raw

        print(
            f"[MINUTE FULL RESULT] {symbol} "
            f"page={page + 1}, rows={len(rows)}, saved={saved}"
        )

        if oldest_time is None:
            break

        if oldest_time <= min_time:
            break

        end_time = decrease_hhmmss(oldest_time, minutes=1)

        import time
        time.sleep(0.15)

    print(f"[MINUTE FULL DONE] {symbol} saved={saved_count}")


def get_cached_minute_window_docs(
    symbol: str,
    before: Optional[str],
    limit: int
) -> List[dict]:
    query = {
        "symbol": symbol,
        "date_key": get_today_key()
    }

    if before:
        query["time_key"] = {"$lt": before}

    return list(
        minute_candle_collection
        .find(query)
        .sort("time_key", -1)
        .limit(limit)
    )


def fetch_minute_candle_window_into_cache(
    symbol: str,
    before: Optional[str],
    required_count: int
):
    symbol = normalize_symbol(symbol)
    delete_old_minute_candles(symbol)

    target_count = max(1, required_count)

    # 최초 요청은 최근 분봉이 오래되었을 때 최신 한 페이지만 먼저 갱신한다.
    if before is None and not is_minute_cache_fresh(symbol, max_delay_minutes=2):
        recent_rows = fetch_korea_minute_page(
            symbol,
            normalize_market_minute_end_time()
        )
        save_minute_rows_to_mongo(symbol, recent_rows)

    docs = get_cached_minute_window_docs(symbol, before, target_count)

    if len(docs) >= target_count:
        return

    if docs:
        oldest_time = docs[-1].get("time_key")
        end_time = decrease_hhmmss(oldest_time, minutes=1)
    elif before:
        end_time = decrease_hhmmss(before, minutes=1)
    else:
        end_time = normalize_market_minute_end_time()

    max_pages = max(2, ((target_count - len(docs)) + 29) // 30 + 1)
    previous_oldest = None

    for page in range(max_pages):
        if len(docs) >= target_count or end_time < "090000":
            break

        print(
            f"[LAZY MINUTE FETCH] {symbol} "
            f"page={page + 1}, end={end_time}"
        )

        rows = fetch_korea_minute_page(symbol, end_time)

        if not rows:
            break

        save_minute_rows_to_mongo(symbol, rows)

        times = [
            row.get("stck_cntg_hour")
            for row in rows
            if row.get("stck_cntg_hour")
        ]

        if not times:
            break

        oldest_time = min(times)

        if previous_oldest == oldest_time:
            break

        previous_oldest = oldest_time
        end_time = decrease_hhmmss(oldest_time, minutes=1)

        docs = get_cached_minute_window_docs(symbol, before, target_count)

        import time
        time.sleep(0.15)


def get_korea_minute_candle_window(
    symbol: str,
    before: Optional[str],
    limit: int
) -> tuple[List[CandlePoint], bool, Optional[str]]:
    symbol = normalize_symbol(symbol)
    fetch_count = limit + 1

    fetch_minute_candle_window_into_cache(
        symbol=symbol,
        before=before,
        required_count=fetch_count
    )

    docs = get_cached_minute_window_docs(symbol, before, fetch_count)

    has_more = len(docs) > limit
    selected_docs = docs[:limit]
    selected_docs.reverse()

    points = []

    for doc in selected_docs:
        point = CandlePoint(
            date=doc.get("date", ""),
            open=parse_int(doc.get("open")),
            high=parse_int(doc.get("high")),
            low=parse_int(doc.get("low")),
            close=parse_int(doc.get("close"))
        )

        if point.open > 0 and point.high > 0 and point.low > 0 and point.close > 0:
            points.append(point)

    next_cursor = selected_docs[0].get("time_key") if selected_docs else None

    return points, has_more, next_cursor


def get_korea_minute_candles(symbol: str, period: str = "1w") -> List[CandlePoint]:
    """
    1분봉 조회.

    KIS 주식당일분봉조회는 당일 분봉만 제공한다.
    따라서 period 값과 상관없이 오늘 하루치 1분봉만 수집한다.
    """
    symbol = normalize_symbol(symbol)

    end_time = normalize_market_minute_end_time()
    min_time = "090000"

    collected_rows: Dict[str, dict] = {}

    max_pages = 6

    for page in range(max_pages):
        print(f"[MINUTE PAGE REQUEST] {symbol} end_time={end_time}")

        rows = fetch_korea_minute_page(symbol, end_time)

        if not rows:
            break

        oldest_time = None

        for row in rows:
            time_raw = row.get("stck_cntg_hour") or ""

            if not time_raw:
                continue

            collected_rows[time_raw] = row

            if oldest_time is None or time_raw < oldest_time:
                oldest_time = time_raw

        print(f"[MINUTE PAGE RESULT] page={page + 1}, rows={len(rows)}, total={len(collected_rows)}")

        if oldest_time is None:
            break

        if oldest_time <= min_time:
            break

        # 다음 호출은 가장 오래된 시간보다 1분 전
        end_time = decrease_hhmmss(oldest_time, minutes=1)

        import time
        time.sleep(0.15)

    sorted_times = sorted(collected_rows.keys())

    points: List[CandlePoint] = []

    for time_key in sorted_times:
        candle = make_minute_candle_from_row(collected_rows[time_key])
        if candle is not None:
            points.append(candle)

    print(f"[MINUTE TOTAL] {symbol} {len(points)} candles")

    return points


# =========================
# 실시간 tick / polling → 1분봉 생성
# =========================

def make_minute_key() -> str:
    return datetime.now().strftime("%H:%M")


def update_realtime_candle(symbol: str, price: int) -> CandlePoint:
    key = make_minute_key()
    old = latest_candles.get(symbol)

    if old is None or old.date != key:
        candle = CandlePoint(
            date=key,
            open=price,
            high=price,
            low=price,
            close=price
        )
    else:
        candle = CandlePoint(
            date=old.date,
            open=old.open,
            high=max(old.high, price),
            low=min(old.low, price),
            close=price
        )

    latest_candles[symbol] = candle
    return candle

def normalize_realtime_price(symbol: str, price: int) -> int:
    last_price = latest_prices.get(symbol)

    if last_price is None or last_price <= 0:
        return price

    # 예: 35,500이어야 하는데 355,000처럼 10배로 들어온 경우 방어
    if price > last_price * 5:
        fixed = price // 10

        if abs(fixed - last_price) < abs(price - last_price):
            print(f"[PRICE FIX x10] {symbol} {price} -> {fixed}")
            return fixed

    return price


def is_abnormal_price(symbol: str, price: int) -> bool:
    last_price = latest_prices.get(symbol)

    if last_price is None or last_price <= 0:
        return False

    diff_rate = abs(price - last_price) / last_price

    # 직전 가격 대비 30% 이상 튀면 이상값으로 무시
    if diff_rate > 0.3:
        print(
            f"[ABNORMAL PRICE SKIP] {symbol} "
            f"last={last_price}, new={price}, diff={diff_rate:.2f}"
        )
        return True

    return False

# =========================
# Unity WebSocket
# =========================

async def broadcast(symbol: str, payload: dict):
    clients = unity_clients.get(symbol, [])
    dead = []

    for ws in clients:
        try:
            await ws.send_text(json.dumps(payload, ensure_ascii=False))
        except Exception:
            dead.append(ws)

    for ws in dead:
        if ws in clients:
            clients.remove(ws)

async def broadcast_orderbook(symbol: str, payload: dict):
    clients = orderbook_unity_clients.get(symbol, [])
    dead_clients = []

    message = json.dumps(payload, ensure_ascii=False)

    for websocket in clients:
        try:
            await websocket.send_text(message)
        except Exception:
            dead_clients.append(websocket)

    for websocket in dead_clients:
        if websocket in clients:
            clients.remove(websocket)

async def broadcast_execution(
    symbol: str,
    item: dict
):
    clients = execution_unity_clients.get(
        symbol,
        []
    )

    if not clients:
        return

    payload = {
        "type": "execution",
        "symbol": symbol,
        "item": item
    }

    message = json.dumps(
        payload,
        ensure_ascii=False
    )

    dead_clients = []

    for websocket in clients:
        try:
            await websocket.send_text(
                message
            )
        except Exception:
            dead_clients.append(
                websocket
            )

    for websocket in dead_clients:
        if websocket in clients:
            clients.remove(websocket)


def format_execution_time(
    raw_time: str
) -> str:
    text = str(raw_time or "").strip()
    text = text.replace(":", "")

    if len(text) < 6:
        return text

    return (
        f"{text[0:2]}:"
        f"{text[2:4]}:"
        f"{text[4:6]}"
    )


def determine_execution_side(
    symbol: str,
    price: int,
    ask_price: int,
    bid_price: int
) -> str:
    """
    체결가가 최우선 매도호가에서 체결되면 매수 체결,
    최우선 매수호가에서 체결되면 매도 체결로 판단한다.

    매수·매도호가로 판단하기 어려운 경우
    직전 체결가 방향을 보조로 사용한다.
    """
    if (
        ask_price > 0 and
        price >= ask_price
    ):
        side = "buy"

    elif (
        bid_price > 0 and
        price <= bid_price
    ):
        side = "sell"

    else:
        previous_price = (
            latest_execution_price.get(
                symbol,
                0
            )
        )

        if (
            previous_price > 0 and
            price > previous_price
        ):
            side = "buy"

        elif (
            previous_price > 0 and
            price < previous_price
        ):
            side = "sell"

        else:
            side = latest_execution_side.get(
                symbol,
                "neutral"
            )

    latest_execution_price[symbol] = price

    if side in ("buy", "sell"):
        latest_execution_side[symbol] = side

    return side


async def broadcast_realtime_execution(
    symbol: str,
    execution_time: str,
    price: int,
    quantity: int,
    cumulative_volume: int,
    ask_price: int,
    bid_price: int
):
    """
    KIS에서 받은 실시간 체결을 Unity로 바로 전달한다.

    서버 DB 저장 없음.
    서버 체결 목록 메모리 저장 없음.
    """
    if (
        price <= 0 or
        quantity <= 0
    ):
        return

    side = determine_execution_side(
        symbol=symbol,
        price=price,
        ask_price=ask_price,
        bid_price=bid_price
    )

    item = {
        "id": make_execution_id(
            symbol=symbol,
            execution_time=execution_time,
            price=price,
            quantity=quantity,
            cumulative_volume=cumulative_volume
        ),
        "symbol": symbol,
        "time": format_execution_time(
            execution_time
        ),
        "price": price,
        "side": side,
        "quantity": quantity,
        "cumulative_volume": (
            cumulative_volume
        )
    }

    await broadcast_execution(
        symbol,
        item
    )

def parse_kis_realtime_orderbook(fields: List[str]) -> Optional[dict]:
    """
    KIS H0UNASP0 국내주식 통합 실시간호가 파싱.

    KRX + NXT 통합 매도/매수 10호가 및 잔량
    """
    if fields is None or len(fields) < 43:
        print(
            f"[ORDERBOOK FIELD COUNT ERROR] "
            f"expected>=43, actual={len(fields) if fields else 0}"
        )
        return None

    symbol = str(fields[0]).strip()

    if len(symbol) != 6 or not symbol.isdigit():
        return None

    asks = []
    bids = []

    # KIS 원본은 매도호가 1호가부터 10호가 순서다.
    for level in range(1, 11):
        price_index = 2 + level       # 3~12
        quantity_index = 22 + level  # 23~32

        price = parse_int(fields[price_index])
        quantity = parse_int(fields[quantity_index])

        if price > 0:
            asks.append({
                "price": price,
                "quantity": quantity,
                "side": "ask",
                "level": level
            })

    # 매수호가 1호가부터 10호가
    for level in range(1, 11):
        price_index = 12 + level      # 13~22
        quantity_index = 32 + level  # 33~42

        price = parse_int(fields[price_index])
        quantity = parse_int(fields[quantity_index])

        if price > 0:
            bids.append({
                "price": price,
                "quantity": quantity,
                "side": "bid",
                "level": level
            })

    # Unity 화면:
    # 매도 10호가가 가장 위, 매도 1호가가 현재가 바로 위
    asks.reverse()

    rows = asks + bids

    return {
        "symbol": symbol,
        "current_price": latest_prices.get(symbol, 0),
        "previous_close": latest_previous_closes.get(symbol, 0),
        "rows": rows
    }

async def send_initial_price(websocket: WebSocket, symbol: str):
    try:
        info = await get_korea_best_price_async(symbol)
        candle = update_realtime_candle(symbol, info.price)

        payload = {
            "symbol": symbol,
            "price": info.price,
            "change": info.change,
            "change_rate": info.change_rate,
            "change_sign": info.change_sign,
            "candle": candle.dict()
        }

        await websocket.send_text(json.dumps(payload, ensure_ascii=False))
        print(f"[INITIAL PRICE SENT] {symbol} {info.price}")

    except Exception as e:
        print("[INITIAL PRICE SEND FAIL]", e)


async def korea_price_polling_loop(symbol: str, interval_sec: float = 2.0):
    symbol = normalize_symbol(symbol)
    last_sent_price = None

    while True:
        try:
            clients = unity_clients.get(
                symbol,
                []
            )

            if len(clients) == 0:
                await asyncio.sleep(
                    interval_sec
                )
                continue

            info = await get_korea_best_price_async(
                symbol
            )

            if info is None:
                await asyncio.sleep(
                    interval_sec
                )
                continue

            price = int(info.price)

            if price > 0 and price != last_sent_price:
                last_sent_price = price
                latest_prices[symbol] = price
                latest_names[symbol] = info.name

                candle = update_realtime_candle(symbol, price)

                payload = {
                    "symbol": symbol,
                    "price": price,
                    "change": info.change,
                    "change_rate": info.change_rate,
                    "change_sign": info.change_sign,
                    "candle": candle.dict()
                }

                await broadcast(symbol, payload)
                print(f"[POLL BEST PRICE SENT] {symbol} {price}")

        except Exception as e:
            print("[PRICE POLLING ERROR]", e)

        await asyncio.sleep(interval_sec)

async def stop_price_sources_if_no_clients(symbol: str):
    global kis_ws_task, kis_ws_symbol, kis_ws_mode

    clients = unity_clients.get(symbol, [])

    if len(clients) > 0:
        return

    # KIS WebSocket 중지
    if kis_ws_symbol == symbol and kis_ws_task is not None and not kis_ws_task.done():
        print(f"[KIS WS STOP - NO UNITY CLIENTS] {symbol}")

        kis_ws_task.cancel()

        try:
            await kis_ws_task
        except asyncio.CancelledError:
            pass
        except Exception as e:
            print("[KIS WS STOP ERROR]", e)

        kis_ws_task = None
        kis_ws_symbol = None
        kis_ws_mode = None

    # REST 가격 polling task 중지
    task = price_polling_tasks.get(symbol)

    if task is not None and not task.done():
        print(f"[POLLING TASK STOP - NO UNITY CLIENTS] {symbol}")

        task.cancel()

        try:
            await task
        except asyncio.CancelledError:
            pass
        except Exception as e:
            print("[POLLING TASK STOP ERROR]", e)

    price_polling_tasks.pop(symbol, None)

@app.websocket("/ws/price/{symbol}")
async def unity_price_socket(websocket: WebSocket, symbol: str):
    symbol = normalize_symbol(symbol)

    await websocket.accept()

    if symbol not in unity_clients:
        unity_clients[symbol] = []

    unity_clients[symbol].append(websocket)

    await send_initial_price(websocket, symbol)

    await start_kis_ws_for_symbol(symbol)

    if symbol not in price_polling_tasks or price_polling_tasks[symbol].done():
        price_polling_tasks[symbol] = asyncio.create_task(
            korea_price_polling_loop(symbol, 2.0)
        )
        print(f"[POLLING TASK STARTED] {symbol}")

    try:
        while True:
            await websocket.receive_text()



    except WebSocketDisconnect:

        if symbol in unity_clients and websocket in unity_clients[symbol]:
            unity_clients[symbol].remove(websocket)

        print(f"[UNITY WS DISCONNECTED] {symbol}")

        await stop_price_sources_if_no_clients(symbol)


    except Exception as e:

        print("[UNITY WS ERROR]", e)

        if symbol in unity_clients and websocket in unity_clients[symbol]:
            unity_clients[symbol].remove(websocket)

        await stop_price_sources_if_no_clients(symbol)

@app.websocket("/ws/orderbook/{symbol}")
async def unity_orderbook_socket(
    websocket: WebSocket,
    symbol: str
):
    symbol = normalize_symbol(symbol)

    await websocket.accept()

    if symbol not in orderbook_unity_clients:
        orderbook_unity_clients[symbol] = []

    orderbook_unity_clients[symbol].append(
        websocket
    )

    print(
        f"[UNITY ORDERBOOK CONNECTED] "
        f"{symbol} / "
        f"clients={len(orderbook_unity_clients[symbol])}"
    )

    # WebSocket 실시간 호가가 도착하기 전에도
    # 화면이 비어 있지 않도록 REST 호가를 한 번 전송한다.
    try:
        initial = await asyncio.to_thread(
            get_orderbook,
            symbol
        )

        best_price_info = await asyncio.to_thread(
            get_korea_best_price,
            symbol
        )

        best_current_price = (
            best_price_info.price
            if best_price_info is not None
            else 0
        )

        best_previous_close = 0

        if best_price_info is not None:
            best_previous_close = (
                    best_price_info.price
                    - best_price_info.change
            )

        if best_previous_close <= 0:
            best_previous_close = (
                initial.get("info", {})
                .get("previous_close", 0)
            )

        initial_payload = {
            "symbol": symbol,
            "current_price": best_current_price,
            "previous_close": best_previous_close,
            "rows": initial.get("rows", [])
        }

        await websocket.send_text(
            json.dumps(
                initial_payload,
                ensure_ascii=False
            )
        )

        print(
            f"[INITIAL ORDERBOOK SENT] "
            f"{symbol}"
        )

    except Exception as error:
        print(
            "[INITIAL ORDERBOOK SEND FAIL]",
            error
        )

    await start_kis_orderbook_ws(symbol)

    try:
        while True:
            await websocket.receive_text()

    except WebSocketDisconnect:
        print(
            f"[UNITY ORDERBOOK DISCONNECTED] "
            f"{symbol}"
        )

    except Exception as error:
        print(
            "[UNITY ORDERBOOK SOCKET ERROR]",
            error
        )

    finally:
        clients = orderbook_unity_clients.get(
            symbol,
            []
        )

        if websocket in clients:
            clients.remove(websocket)

        await stop_orderbook_source_if_no_clients(
            symbol
        )

@app.websocket("/ws/executions/{symbol}")
async def unity_execution_socket(
    websocket: WebSocket,
    symbol: str
):
    symbol = normalize_symbol(symbol)

    await websocket.accept()

    if symbol not in execution_unity_clients:
        execution_unity_clients[symbol] = []

    execution_unity_clients[symbol].append(
        websocket
    )

    print(
        f"[UNITY EXECUTION CONNECTED] "
        f"{symbol} / "
        f"clients="
        f"{len(execution_unity_clients[symbol])}"
    )

    try:
        while True:
            # Unity가 별도 메시지를 보내지 않아도
            # 연결 종료 감지를 위해 대기한다.
            await websocket.receive_text()

    except WebSocketDisconnect:
        print(
            f"[UNITY EXECUTION DISCONNECTED] "
            f"{symbol}"
        )

    except Exception as error:
        print(
            "[UNITY EXECUTION SOCKET ERROR]",
            error
        )

    finally:
        clients = execution_unity_clients.get(
            symbol,
            []
        )

        if websocket in clients:
            clients.remove(websocket)

        if not clients:
            execution_unity_clients.pop(
                symbol,
                None
            )

async def start_kis_ws_for_symbol(symbol: str):
    global kis_ws_task, kis_ws_symbol, kis_ws_mode

    symbol = normalize_symbol(symbol)
    tr_id = get_realtime_tr_id()

    if kis_ws_task is not None and not kis_ws_task.done():
        if kis_ws_symbol == symbol and kis_ws_mode == tr_id:
            return

        print(f"[KIS WS CANCEL] {kis_ws_symbol} / {kis_ws_mode} -> {symbol} / {tr_id}")
        kis_ws_task.cancel()

        try:
            await kis_ws_task
        except asyncio.CancelledError:
            pass
        except Exception:
            pass

    kis_ws_symbol = symbol
    kis_ws_mode = tr_id
    kis_ws_task = asyncio.create_task(kis_korea_ws_loop(symbol, tr_id))

    print(f"[KIS WS TASK STARTED] {symbol} / {tr_id}")


# =========================
# KIS WebSocket - 정규장/시간외 실시간 체결
# =========================

async def kis_korea_ws_loop(symbol: str = "005930", tr_id: str = "H0UNCNT0"):
    symbol = normalize_symbol(symbol)
    approval_key = get_approval_key()

    subscribe_message = {
        "header": {
            "approval_key": approval_key,
            "custtype": "P",
            "tr_type": "1",
            "content-type": "utf-8"
        },
        "body": {
            "input": {
                "tr_id": tr_id,
                "tr_key": symbol
            }
        }
    }

    last_sent_price = None

    try:
        print(f"[KIS WS CONNECT] {symbol} / {tr_id}")

        async with websockets.connect(KIS_WS_URL, ping_interval=None) as ws:
            await ws.send(json.dumps(subscribe_message))

            async for message in ws:
                if not message:
                    continue

                if message.startswith("{"):
                    print("[KIS JSON]", message)

                    try:
                        data = json.loads(message)
                        json_tr_id = data.get("header", {}).get("tr_id")

                        if json_tr_id == "PINGPONG":
                            await ws.send(message)
                            print("[KIS PINGPONG SENT]")
                    except Exception as e:
                        print("[PINGPONG HANDLE FAIL]", e)

                    continue

                parts = message.split("|")

                if len(parts) < 4:
                    continue

                recv_tr_id = parts[1]
                body = parts[3]

                # KRX / KRX 시간외 / NXT / 통합 실시간 체결가 모두 허용
                if recv_tr_id not in ["H0STCNT0", "H0STOUP0", "H0NXCNT0", "H0UNCNT0"]:
                    continue

                fields = body.split("^")

                try:
                    recv_symbol = str(
                        fields[0]
                    ).strip()

                    execution_time = str(
                        fields[1]
                    ).strip()

                    price = parse_int(
                        fields[2]
                    )

                    # 통합 실시간체결 응답에서
                    # 최우선 매도·매수호가
                    ask_price = (
                        parse_int(fields[10])
                        if len(fields) > 10
                        else 0
                    )

                    bid_price = (
                        parse_int(fields[11])
                        if len(fields) > 11
                        else 0
                    )

                    # 현재 체결량
                    execution_quantity = (
                        parse_int(fields[12])
                        if len(fields) > 12
                        else 0
                    )

                    # 누적 거래량
                    cumulative_volume = (
                        parse_int(fields[13])
                        if len(fields) > 13
                        else 0
                    )

                    print(
                        "[H0UNCNT0 EXECUTION]",
                        {
                            "symbol": recv_symbol,
                            "time": execution_time,
                            "price": price,
                            "ask": ask_price,
                            "bid": bid_price,
                            "quantity": execution_quantity,
                            "cumulative_volume": cumulative_volume
                        }
                    )

                    if recv_tr_id == "H0UNCNT0":
                        print(
                            "[H0UNCNT0 EXECUTION]",
                            {
                                "symbol": recv_symbol,
                                "time": execution_time,
                                "price": price,
                                "ask": ask_price,
                                "bid": bid_price,
                                "quantity": execution_quantity
                            }
                        )

                except Exception as error:
                    print(
                        "[WS PARSE FAIL]",
                        recv_tr_id,
                        error,
                        fields
                    )
                    continue

                except Exception:
                    print("[WS PARSE FAIL]", recv_tr_id, fields)
                    continue

                if recv_symbol != symbol:
                    continue

                if price <= 0:
                    continue

                await broadcast_realtime_execution(
                    symbol=symbol,
                    execution_time=execution_time,
                    price=price,
                    quantity=execution_quantity,
                    cumulative_volume=cumulative_volume,
                    ask_price=ask_price,
                    bid_price=bid_price
                )

                price = normalize_realtime_price(symbol, price)

                if price <= 0:
                    continue

                if is_abnormal_price(symbol, price):
                    continue

                if last_sent_price == price:
                    continue

                last_sent_price = price
                latest_prices[symbol] = price

                candle = update_realtime_candle(symbol, price)

                payload = {
                    "symbol": symbol,
                    "price": price,
                    "change": 0,
                    "change_rate": 0.0,
                    "change_sign": "0",
                    "candle": candle.dict()
                }

                await broadcast(symbol, payload)

                if recv_tr_id == "H0UNCNT0":
                    print(f"[KIS TOTAL WS PRICE SENT] {symbol} {price}")
                elif recv_tr_id == "H0NXCNT0":
                    print(f"[KIS NXT WS PRICE SENT] {symbol} {price}")
                elif recv_tr_id == "H0STOUP0":
                    print(f"[KIS KRX AFTER-HOURS WS PRICE SENT] {symbol} {price}")
                else:
                    print(f"[KIS KRX REGULAR WS PRICE SENT] {symbol} {price}")

    except asyncio.CancelledError:
        print(f"[KIS WS CANCELLED] {symbol} / {tr_id}")
        raise

    except Exception as e:
        print("[KIS WS ERROR]", e)

async def kis_orderbook_ws_loop(symbol: str):
    symbol = normalize_symbol(symbol)
    approval_key = get_approval_key()

    # KRX 전용 H0STASP0이 아니라 통합 호가
    tr_id = "H0UNASP0"

    subscribe_message = {
        "header": {
            "approval_key": approval_key,
            "custtype": "P",
            "tr_type": "1",
            "content-type": "utf-8"
        },
        "body": {
            "input": {
                "tr_id": tr_id,
                "tr_key": symbol
            }
        }
    }

    reconnect_delay = 1.0

    while True:
        try:
            print(
                f"[KIS INTEGRATED ORDERBOOK WS CONNECT] "
                f"{symbol} / {tr_id}"
            )

            async with websockets.connect(
                KIS_WS_URL,
                ping_interval=None
            ) as websocket:

                await websocket.send(
                    json.dumps(subscribe_message)
                )

                reconnect_delay = 1.0

                async for message in websocket:
                    if not message:
                        continue

                    if message.startswith("{"):
                        try:
                            data = json.loads(message)

                            json_tr_id = (
                                data.get("header", {})
                                .get("tr_id")
                            )

                            if json_tr_id == "PINGPONG":
                                await websocket.send(message)

                        except Exception as error:
                            print(
                                "[ORDERBOOK JSON HANDLE FAIL]",
                                error
                            )

                        continue

                    parts = message.split("|")

                    if len(parts) < 4:
                        continue

                    recv_tr_id = parts[1]

                    if recv_tr_id != "H0UNASP0":
                        continue

                    fields = parts[3].split("^")

                    payload = parse_kis_realtime_orderbook(
                        fields
                    )

                    if payload is None:
                        continue

                    if payload["symbol"] != symbol:
                        continue

                    await broadcast_orderbook(
                        symbol,
                        payload
                    )

        except asyncio.CancelledError:
            print(
                f"[KIS INTEGRATED ORDERBOOK CANCELLED] "
                f"{symbol}"
            )
            raise

        except Exception as error:
            print(
                "[KIS INTEGRATED ORDERBOOK ERROR]",
                error
            )

            await asyncio.sleep(reconnect_delay)

            reconnect_delay = min(
                reconnect_delay * 2.0,
                30.0
            )

async def start_kis_orderbook_ws(symbol: str):
    global orderbook_kis_ws_task
    global orderbook_kis_ws_symbol

    symbol = normalize_symbol(symbol)

    if (
        orderbook_kis_ws_task is not None
        and not orderbook_kis_ws_task.done()
    ):
        if orderbook_kis_ws_symbol == symbol:
            return

        print(
            f"[KIS ORDERBOOK WS CHANGE] "
            f"{orderbook_kis_ws_symbol} -> {symbol}"
        )

        orderbook_kis_ws_task.cancel()

        try:
            await orderbook_kis_ws_task
        except asyncio.CancelledError:
            pass
        except Exception as error:
            print(
                "[ORDERBOOK OLD TASK CLOSE ERROR]",
                error
            )

    orderbook_kis_ws_symbol = symbol

    orderbook_kis_ws_task = asyncio.create_task(
        kis_orderbook_ws_loop(symbol)
    )

    print(
        f"[KIS ORDERBOOK TASK STARTED] "
        f"{symbol}"
    )


async def stop_orderbook_source_if_no_clients(
    symbol: str
):
    global orderbook_kis_ws_task
    global orderbook_kis_ws_symbol

    clients = orderbook_unity_clients.get(
        symbol,
        []
    )

    if len(clients) > 0:
        return

    if (
        orderbook_kis_ws_symbol == symbol
        and orderbook_kis_ws_task is not None
        and not orderbook_kis_ws_task.done()
    ):
        print(
            f"[KIS ORDERBOOK STOP - NO CLIENTS] "
            f"{symbol}"
        )

        orderbook_kis_ws_task.cancel()

        try:
            await orderbook_kis_ws_task
        except asyncio.CancelledError:
            pass
        except Exception as error:
            print(
                "[KIS ORDERBOOK STOP ERROR]",
                error
            )

    orderbook_kis_ws_task = None
    orderbook_kis_ws_symbol = None

    orderbook_unity_clients.pop(
        symbol,
        None
    )

# =========================
# 포트폴리오 / 거래
# =========================

def get_user_portfolio(user_id: str) -> dict:
    portfolio = portfolios_collection.find_one({"user_id": user_id})

    if portfolio is None:
        portfolio = {
            "user_id": user_id,
            "cash": DEFAULT_CASH,
            "holdings": {},
            "created_at": datetime.now(),
            "updated_at": datetime.now()
        }

        portfolios_collection.insert_one(portfolio)

    return portfolio


def save_user_portfolio(user_id: str, cash: int, holdings: dict):
    portfolios_collection.update_one(
        {"user_id": user_id},
        {
            "$set": {
                "cash": int(cash),
                "holdings": holdings,
                "updated_at": datetime.now()
            },
            "$setOnInsert": {
                "user_id": user_id,
                "created_at": datetime.now()
            }
        },
        upsert=True
    )


def save_trade_log(
    user_id: str,
    action: str,
    symbol: str,
    quantity: int,
    price: int,
    cash_after_trade: int
):
    trade_logs_collection.insert_one({
        "user_id": user_id,
        "action": action,
        "symbol": symbol,
        "quantity": int(quantity),
        "price": int(price),
        "total": int(price) * int(quantity),
        "cash_after_trade": int(cash_after_trade),
        "created_at": datetime.now()
    })


def get_current_price(symbol: str) -> int:
    symbol = normalize_symbol(symbol)

    if symbol in latest_prices:
        return latest_prices[symbol]

    info = get_korea_best_price(symbol)
    return info.price



# =========================
# KIS 종목 마스터(MST)
# =========================

def get_mst_last_update_date() -> Optional[str]:
    data = load_json_file(str(MST_UPDATE_MARKER_PATH))

    if not data:
        return None

    return str(data.get("updated_date", "")).strip() or None


def is_mst_update_needed() -> bool:
    """
    KOSPI/KOSDAQ 파일이 모두 있고 오늘 이미 갱신했다면 다운로드를 생략한다.
    """
    if not KOSPI_MST_PATH.exists() or not KOSDAQ_MST_PATH.exists():
        return True

    return get_mst_last_update_date() != datetime.now().strftime("%Y-%m-%d")


def download_and_replace_mst(
    download_url: str,
    destination_path: Path,
    expected_member_name: str
):
    """
    KIS 공식 ZIP을 메모리로 내려받아 검사한 뒤 기존 MST를 원자적으로 교체한다.
    다운로드나 압축 해제가 실패하면 기존 파일은 건드리지 않는다.
    """
    print(f"[MST DOWNLOAD START] {expected_member_name}")

    response = requests.get(download_url, timeout=30)
    response.raise_for_status()

    with zipfile.ZipFile(io.BytesIO(response.content)) as archive:
        member_name = next(
            (
                name
                for name in archive.namelist()
                if Path(name).name.lower() == expected_member_name.lower()
            ),
            None
        )

        if member_name is None:
            raise RuntimeError(
                f"{expected_member_name} not found in downloaded ZIP"
            )

        mst_bytes = archive.read(member_name)

    # 비정상 HTML/빈 파일 등이 기존 정상 파일을 덮어쓰지 않도록 최소 크기 검사
    if len(mst_bytes) < 10_000:
        raise RuntimeError(
            f"downloaded MST is too small: {len(mst_bytes):,} bytes"
        )

    temporary_path = destination_path.with_suffix(
        destination_path.suffix + ".download"
    )

    try:
        temporary_path.write_bytes(mst_bytes)
        temporary_path.replace(destination_path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink(missing_ok=True)

    print(
        f"[MST DOWNLOAD SUCCESS] {destination_path.name} "
        f"size={len(mst_bytes):,} bytes"
    )


def update_mst_files_if_needed(force: bool = False) -> bool:
    """
    서버 시작 시 하루 한 번 KIS 공식 종목 마스터를 갱신한다.

    - 저장 위치: main_kr_mongo2.py와 같은 폴더
    - 갱신 실패: 기존 MST를 그대로 사용
    - force=True: 오늘 갱신했어도 다시 다운로드
    """
    if not force and not is_mst_update_needed():
        print(
            f"[MST UPDATE SKIP] already updated today "
            f"({get_mst_last_update_date()})"
        )
        return False

    errors = []

    targets = [
        (
            KOSPI_MST_DOWNLOAD_URL,
            KOSPI_MST_PATH,
            "kospi_code.mst"
        ),
        (
            KOSDAQ_MST_DOWNLOAD_URL,
            KOSDAQ_MST_PATH,
            "kosdaq_code.mst"
        )
    ]

    for download_url, destination_path, member_name in targets:
        try:
            download_and_replace_mst(
                download_url=download_url,
                destination_path=destination_path,
                expected_member_name=member_name
            )
        except Exception as error:
            errors.append(f"{member_name}: {error}")
            print(f"[MST DOWNLOAD FAIL] {member_name}: {error}")

    if errors:
        # 일부만 성공했더라도 날짜 마커를 기록하지 않아 다음 시작 때 재시도한다.
        raise RuntimeError(" / ".join(errors))

    save_json_file(str(MST_UPDATE_MARKER_PATH), {
        "updated_date": datetime.now().strftime("%Y-%m-%d"),
        "updated_at": datetime.now().isoformat(timespec="seconds"),
        "kospi_url": KOSPI_MST_DOWNLOAD_URL,
        "kosdaq_url": KOSDAQ_MST_DOWNLOAD_URL
    })

    print("[MST UPDATE COMPLETE]")
    return True

def parse_kis_mst_file(file_path: Path, market: str) -> List[Dict[str, str]]:
    """
    KIS 국내주식 MST 파일에서 검색에 필요한 앞 63바이트만 파싱한다.

    앞부분 구조:
    - 단축코드: 9바이트
    - 표준코드: 12바이트
    - 한글종목명: 40바이트(CP949)
    - 증권그룹구분코드: 2바이트
    """
    stocks: List[Dict[str, str]] = []

    if not file_path.exists():
        print(f"[MST FILE NOT FOUND] {file_path}")
        return stocks

    try:
        with file_path.open("rb") as mst_file:
            for raw_line in mst_file:
                raw_line = raw_line.rstrip(b"\r\n")

                if len(raw_line) < 63:
                    continue

                symbol = raw_line[0:9].decode("cp949", errors="ignore").strip()
                standard_code = raw_line[9:21].decode("cp949", errors="ignore").strip()
                name = raw_line[21:61].decode("cp949", errors="ignore").strip()
                security_group = raw_line[61:63].decode("ascii", errors="ignore").strip()

                # Unity/KIS 현재가 API에서 사용하는 국내주식 6자리 코드만 적재한다.
                if len(symbol) != 6 or not symbol.isdigit() or not name:
                    continue

                stocks.append({
                    "symbol": symbol,
                    "name": name,
                    "market": market,
                    "standard_code": standard_code,
                    "security_group": security_group
                })

    except Exception as e:
        print(f"[MST PARSE FAIL] {file_path.name}: {e}")
        return []

    print(f"[MST LOAD] {market}: {len(stocks):,} stocks")
    return stocks


def load_kis_stock_master() -> int:
    """
    kospi_code.mst와 kosdaq_code.mst를 읽어 전체 종목을 메모리에 적재한다.
    MongoDB나 외부 검색 API를 사용하지 않는다.
    """
    stock_master.clear()

    all_stocks = (
        parse_kis_mst_file(KOSPI_MST_PATH, "KOSPI")
        + parse_kis_mst_file(KOSDAQ_MST_PATH, "KOSDAQ")
    )

    for stock in all_stocks:
        stock_master[stock["symbol"]] = stock
        latest_names[stock["symbol"]] = stock["name"]

    print(f"[MST STOCK MASTER READY] total={len(stock_master):,}")
    return len(stock_master)


# =========================
# Startup
# =========================

@app.on_event("startup")
async def startup_event():
    try:
        mongo_client.admin.command("ping")

        print(
            f"[MONGO CONNECTED] "
            f"database={MONGODB_DB}"
        )

        # Partial username index and shared account indexes are set by the engine.
        app_trading.initialize()

        portfolios_collection.create_index(
            [("user_id", 1)],
            unique=True
        )

        trade_logs_collection.create_index([
            ("user_id", 1),
            ("created_at", -1)
        ])

    except Exception as error:
        print(
            "[MONGO CONNECTION FAILED]",
            error
        )
        raise

    # 인덱스 생성은 외부 API 상태와 관계없이 먼저 완료한다.
    try:
        # 레거시 종목 검색 데이터가 존재할 때만 활용한다.
        krx_daily_collection.create_index([("date", 1), ("symbol", 1)])
        krx_daily_collection.create_index([("name", 1)])

        candle_cache_collection.create_index(
            [("symbol", 1), ("interval", 1), ("date_key", 1)],
            unique=True
        )

        minute_candle_collection.create_index(
            [("symbol", 1), ("date_key", 1), ("time_key", 1)],
            unique=True
        )

        candle_cache_collection.create_index(
            "expires_at",
            expireAfterSeconds=0
        )
        minute_candle_collection.create_index(
            "expires_at",
            expireAfterSeconds=0
        )

        default_expire_at = datetime.now() + CANDLE_CACHE_EXPIRE_DELTA
        candle_cache_collection.update_many(
            {"expires_at": {"$exists": False}},
            {"$set": {"expires_at": default_expire_at}}
        )
        minute_candle_collection.update_many(
            {"expires_at": {"$exists": False}},
            {"$set": {"expires_at": default_expire_at}}
        )

        print("[MONGO INDEX READY]")
    except Exception as e:
        print("[MONGO STARTUP WARNING]", e)

    # 하루 한 번 KIS 공식 서버에서 최신 MST를 받은 뒤 메모리에 적재한다.
    # 다운로드 실패 시 기존 로컬 파일을 그대로 사용한다.
    try:
        update_mst_files_if_needed()
    except Exception as e:
        print(f"[MST AUTO UPDATE WARNING] {e}")
        print("[MST FALLBACK] existing local MST files will be used")

    try:
        loaded_count = load_kis_stock_master()

        if loaded_count == 0:
            print(
                "[MST STOCK MASTER WARNING] "
                "kospi_code.mst / kosdaq_code.mst 파일 위치를 확인하세요."
            )
    except Exception as e:
        print("[MST STOCK MASTER LOAD WARNING]", e)

    try:
        get_access_token()
        get_approval_key()
        print(f"[SERVER READY] KIS mode={KIS_MODE}")
    except Exception as e:
        print("[KIS STARTUP WARNING]", e)
        print("[SERVER READY] token will be requested later")


# =========================
# API
# =========================

@app.get(
    "/stock-info",
    response_model=StockInfoResponse
)
def stock_info(
    symbol: str = Query("005930")
):
    symbol = normalize_symbol(symbol)

    # 표시할 대표 현재가는 NXT 우선 정책 유지
    info = get_korea_best_price(symbol)

    # 시가·고가·저가·거래량은
    # KRX 현재가 조회 응답을 기준으로 보완
    regular_info = safe_get_krx_price(symbol)

    if regular_info is not None:
        info.open_price = regular_info.open_price
        info.high_price = regular_info.high_price
        info.low_price = regular_info.low_price
        info.volume = regular_info.volume

    stock_data = stock_master.get(
        symbol,
        {}
    )

    stock_name = str(
        stock_data.get("name")
        or info.name
        or symbol
    )

    market = str(
        stock_data.get("market")
        or ""
    )

    info.name = stock_name
    info.market = market

    latest_names[symbol] = stock_name

    return info


@app.get("/candles/window", response_model=CandleWindowResponse)
def get_candle_window(
    symbol: str = Query("005930"),
    interval: str = Query("1d"),
    limit: int = Query(200, ge=10, le=500),
    before: Optional[str] = Query(None)
):
    """
    현재 화면에 필요한 캔들만 반환한다.

    - 최초 요청: before 없이 최근 limit개
    - 과거 추가 요청: 응답의 next_cursor를 before에 전달
    - DB에 없는 구간만 KIS에서 받아 저장
    """
    symbol = normalize_symbol(symbol)

    allowed_intervals = {"1m", "1d", "1wk", "1mo", "1y"}

    if interval not in allowed_intervals:
        raise HTTPException(status_code=400, detail="Unsupported candle interval")

    if before:
        expected_length = 6 if interval == "1m" else 8

        if len(before) != expected_length or not before.isdigit():
            raise HTTPException(status_code=400, detail="Invalid candle cursor")

    if interval == "1m":
        points, has_more, next_cursor = get_korea_minute_candle_window(
            symbol=symbol,
            before=before,
            limit=limit
        )
    else:
        points, has_more, next_cursor = get_korea_daily_candle_window(
            symbol=symbol,
            interval=interval,
            before=before,
            limit=limit
        )

    if not points:
        raise HTTPException(status_code=404, detail="No candle data")

    return CandleWindowResponse(
        symbol=symbol,
        interval=interval,
        points=points,
        has_more=has_more,
        next_cursor=next_cursor
    )


@app.get("/candles", response_model=CandleResponse)
def get_candles(
    symbol: str = Query("005930"),
    period: str = Query("all"),
    interval: str = Query("1d")
):
    symbol = normalize_symbol(symbol)

    if interval == "1m":
        period = "1d"

        cache_key = f"{symbol}:1m"

        if cache_key not in minute_candle_locks:
            minute_candle_locks[cache_key] = Lock()

        points = get_cached_minute_candles(symbol)

        # DB가 있고, 최신이면 바로 반환
        if points and is_minute_cache_fresh(symbol, max_delay_minutes=2):
            print(f"[MINUTE DB FRESH RETURN] {symbol} count={len(points)}")
            return CandleResponse(
                symbol=symbol,
                points=points
            )

        lock = minute_candle_locks[cache_key]

        # 이미 갱신 중이면 기존 DB라도 바로 반환
        if not lock.acquire(blocking=False):
            print(f"[MINUTE UPDATE SKIP - ALREADY LOADING] {symbol}")

            return CandleResponse(
                symbol=symbol,
                points=points
            )

        try:
            print(f"[MINUTE DB STALE OR EMPTY - UPDATE] {symbol}")
            update_today_minute_candles(symbol)

            points = get_cached_minute_candles(symbol)

            return CandleResponse(
                symbol=symbol,
                points=points
            )

        finally:
            lock.release()

    else:
        points = get_korea_daily_candles_cached(symbol, period, interval)

        if not points:
            raise HTTPException(status_code=404, detail="No candle data")

        return CandleResponse(
            symbol=symbol,
            points=points
        )


@app.get("/portfolio", response_model=PortfolioResponse)
def get_portfolio(user_id: str = Depends(get_current_user_id)):
    return app_trading.portfolio(user_id)


def submit_app_trade(req: TradeRequest, user_id: str, side: str):
    order = app_trading.create(user_id, OrderInput(
        symbol=normalize_symbol(req.symbol),
        side=side,
        orderType=req.order_type,
        quantity=req.quantity,
        limitPrice=req.limit_price,
        clientRequestId=req.client_request_id or str(uuid.uuid4()),
    ))
    portfolio = app_trading.portfolio(user_id)
    return {
        "message": "주문 체결 완료" if order["status"] == "FILLED" else "미체결 지정가 주문 접수 완료",
        "symbol": order["symbol"], "quantity": order["quantity"],
        "price": order.get("executedPrice") or 0, "cash": portfolio["cash"],
        "order_id": order["_id"], "status": order["status"],
        "filled_quantity": order["filledQuantity"],
    }


@app.post("/buy", response_model=TradeResponse)
def buy_stock(req: TradeRequest, user_id: str = Depends(get_current_user_id)):
    return submit_app_trade(req, user_id, "BUY")


@app.post("/sell", response_model=TradeResponse)
def sell_stock(req: TradeRequest, user_id: str = Depends(get_current_user_id)):
    return submit_app_trade(req, user_id, "SELL")


@app.get("/trade-logs")
def get_trade_logs(user_id: str = Depends(get_current_user_id)):
    rows = app_trading.orders.find({"userId": user_id, "status": "FILLED"}).sort("executedAt", -1).limit(100)
    return [dict(public(row), action=row["side"].lower(), user_id=user_id,
                 price=row.get("executedPrice") or 0,
                 total=(row.get("executedPrice") or 0) * row["filledQuantity"],
                 created_at=row.get("executedAt")) for row in rows]

@app.get("/warmup-candles")
def warmup_candles(symbol: str = Query("005930")):
    """
    하위 호환용 제한 워밍업.

    이전처럼 20년치 전체 데이터를 받지 않고 각 주기의 최근 구간만 준비한다.
    새 Unity 클라이언트는 이 API를 호출하지 않고 /candles/window를 직접 사용한다.
    """
    symbol = normalize_symbol(symbol)

    limits = {
        "1d": 200,
        "1wk": 160,
        "1mo": 120,
        "1y": 60
    }

    result = {}

    for candle_interval, candle_limit in limits.items():
        try:
            points, has_more, next_cursor = get_korea_daily_candle_window(
                symbol=symbol,
                interval=candle_interval,
                before=None,
                limit=candle_limit
            )

            result[candle_interval] = {
                "success": True,
                "count": len(points),
                "has_more": has_more,
                "next_cursor": next_cursor
            }

        except Exception as e:
            print(f"[LIMITED WARMUP ERROR] {symbol} {candle_interval}: {e}")
            result[candle_interval] = {
                "success": False,
                "error": str(e)
            }

    return {
        "symbol": symbol,
        "message": "limited warmup only",
        "result": result
    }


async def get_korea_best_price_async(symbol: str) -> StockInfoResponse:
    return await asyncio.to_thread(get_korea_best_price, symbol)

@app.get("/stocks/search", response_model=StockRankResponse)
def search_stocks(
    keyword: str = Query(...),
    limit: int = Query(30, ge=1, le=100)
):
    """
    KIS MST에서 읽은 KOSPI·KOSDAQ 전체 종목을 메모리에서 검색한다.

    검색 우선순위:
    1. 종목코드/종목명 완전 일치
    2. 종목코드 앞부분 일치
    3. 종목명 앞부분 일치
    4. 종목코드/종목명 포함
    """
    search_keyword = keyword.strip().lower()

    if not search_keyword:
        return {"items": []}

    exact_matches: List[dict] = []
    code_prefix_matches: List[dict] = []
    name_prefix_matches: List[dict] = []
    contains_matches: List[dict] = []

    for symbol, info in stock_master.items():
        name = str(info.get("name", symbol)).strip()
        symbol_lower = symbol.lower()
        name_lower = name.lower()

        item = {
            "symbol": symbol,
            "name": name,
            "price": latest_prices.get(symbol, 0),
            "change_rate": 0.0,
            "volume": 0,
            "trade_value": 0,
            "market_cap": 0,
            "execution_strength": 0.0,
            "rank": 0
        }

        if search_keyword == symbol_lower or search_keyword == name_lower:
            exact_matches.append(item)
        elif symbol_lower.startswith(search_keyword):
            code_prefix_matches.append(item)
        elif name_lower.startswith(search_keyword):
            name_prefix_matches.append(item)
        elif search_keyword in symbol_lower or search_keyword in name_lower:
            contains_matches.append(item)

    rows = (
        exact_matches
        + code_prefix_matches
        + name_prefix_matches
        + contains_matches
    )[:limit]

    for index, row in enumerate(rows, start=1):
        row["rank"] = index

    return {"items": rows}


@app.get("/stocks/top-volume")
def top_volume(limit: int = Query(30, ge=1, le=30)):
    """
    KIS 국내주식 거래량순위.

    - KRX 국내주식
    - 누적 거래량 기준
    - MongoDB 저장 없음
    - 요청할 때마다 KIS 실전 API 직접 조회
    """
    items = get_kis_volume_rank(limit=limit)

    return {
        "source": "KIS",
        "sort": "volume",
        "realtime": True,
        "as_of": datetime.now().isoformat(timespec="seconds"),
        "items": items
    }


@app.get("/stocks/top-trade-value")
def top_trade_value_legacy(limit: int = Query(30, ge=1, le=30)):
    """
    이전 Unity 주소 호환용.
    실제 순위는 거래대금이 아니라 거래량 기준이다.
    """
    return top_volume(limit)

@app.get("/stocks/rank")
def stock_rank(
    type: str = Query("volume"),
    limit: int = Query(30, ge=1, le=30)
):
    """
    국내주식 순위 통합 API.

    type:
    - volume
    - change-rate
    - market-cap
    - execution-strength
    """
    rank_type = type.strip().lower()

    items = get_kis_stock_rank(
        rank_type=rank_type,
        limit=limit
    )

    return {
        "source": "KIS",
        "ranking": rank_type,
        "realtime": True,
        "as_of": datetime.now().isoformat(timespec="seconds"),
        "items": items
    }

def get_latest_legacy_stock_date() -> Optional[str]:
    doc = krx_daily_collection.find_one({}, sort=[("date", -1)])

    if doc is None:
        return None

    return doc.get("date")


def load_legacy_stock_master_from_mongo() -> int:
    """
    과거에 저장해 둔 종목명 데이터를 검색용으로만 메모리에 적재한다.
    외부 KRX API는 호출하지 않는다.
    """
    stock_master.clear()

    latest_date = get_latest_legacy_stock_date()
    if not latest_date:
        print("[LEGACY STOCK MASTER EMPTY]")
        return 0

    cursor = krx_daily_collection.find(
        {"date": latest_date},
        {"_id": 0, "symbol": 1, "name": 1, "market": 1}
    )

    for row in cursor:
        symbol = str(row.get("symbol", "")).strip()
        name = str(row.get("name", "")).strip()

        if len(symbol) != 6 or not symbol.isdigit() or not name:
            continue

        stock_master[symbol] = {
            "name": name,
            "market": row.get("market", "")
        }

    print(f"[LEGACY STOCK MASTER LOADED] {len(stock_master)}개 / date={latest_date}")
    return len(stock_master)

@app.get("/executions/history")
def get_execution_history(
    symbol: str = Query("005930"),
    before_time: Optional[str] = Query(None),
    limit: int = Query(30, ge=1, le=30)
):
    """
    국내주식 당일 체결내역.

    최초:
    /executions/history?symbol=005930&limit=30

    추가:
    /executions/history
        ?symbol=005930
        &before_time=101530
        &limit=30

    서버 및 MongoDB에는 저장하지 않는다.
    """
    return get_korea_execution_history(
        symbol=symbol,
        before_time=before_time,
        limit=limit
    )

# =========================
# KIS REST - 국내주식 순위
# =========================

RANK_TYPE_VOLUME = "volume"
RANK_TYPE_CHANGE_RATE = "change-rate"
RANK_TYPE_MARKET_CAP = "market-cap"
RANK_TYPE_EXECUTION_STRENGTH = "execution-strength"


def get_rank_stock_name(row: dict, symbol: str) -> str:
    """
    순위 API마다 종목명 필드가 다를 수 있어 공통 처리한다.
    """
    return str(
        row.get("hts_kor_isnm")
        or row.get("stck_kor_isnm")
        or row.get("prdt_name")
        or row.get("kor_isnm")
        or stock_master.get(symbol, {}).get("name")
        or latest_names.get(symbol)
        or symbol
    ).strip()


def get_rank_stock_symbol(row: dict) -> str:
    """
    KIS 순위 API마다 종목코드 필드명이 다르므로 후보를 모두 확인한다.
    """
    symbol = str(
        row.get("mksc_shrn_iscd")
        or row.get("stck_shrn_iscd")
        or row.get("sht_cd")
        or row.get("pdno")
        or ""
    ).strip()

    if len(symbol) == 6 and symbol.isdigit():
        return symbol

    return ""


def make_common_rank_item(row: dict, rank: int) -> Optional[dict]:
    symbol = get_rank_stock_symbol(row)

    if not symbol:
        return None

    name = get_rank_stock_name(row, symbol)

    price = parse_int(
        row.get("stck_prpr")
        or row.get("stck_prc")
        or row.get("prpr")
        or row.get("price")
    )

    change_rate = parse_float(
        row.get("prdy_ctrt")
        or row.get("prdy_vrss_rt")
        or row.get("flng_cls_code")
        or row.get("change_rate")
    )

    volume = parse_int(
        row.get("acml_vol")
        or row.get("acml_tr_pbmn")
        or row.get("vol")
        or row.get("volume")
    )

    trade_value = parse_int(
        row.get("acml_tr_pbmn")
        or row.get("trade_value")
        or row.get("tr_pbmn")
    )

    market_cap = parse_int(
        row.get("stck_avls")
        or row.get("hts_avls")
        or row.get("mket_cap")
        or row.get("market_cap")
    )

    execution_strength = parse_float(
        row.get("tday_rltv")
        or row.get("cttr")
        or row.get("cnqn")
        or row.get("execution_strength")
    )

    if price > 0:
        latest_prices[symbol] = price

    if name and name != symbol:
        latest_names[symbol] = name

    return {
        "symbol": symbol,
        "name": name,
        "price": price,
        "change_rate": change_rate,
        "volume": volume,
        "trade_value": trade_value,
        "market_cap": market_cap,
        "execution_strength": execution_strength,
        "rank": rank
    }


def parse_rank_rows(data: dict) -> List[dict]:
    """
    API에 따라 output 또는 output1로 반환될 수 있어 공통 처리한다.
    """
    rows = (
        data.get("output")
        or data.get("output1")
        or data.get("output2")
        or []
    )

    if isinstance(rows, dict):
        rows = [rows]

    if not isinstance(rows, list):
        return []

    return rows


def request_kis_volume_rank(
    sort_code: str = "0",
    limit: int = 30
) -> List[dict]:
    """
    KIS 국내주식 거래량순위.

    FID_BLNG_CLS_CODE
    0: 거래량 기준
    1: 거래량 증가율
    3: 거래대금 기준
    4: 체결강도 기준으로 사용 시도
    """
    if KIS_MODE != "real":
        raise HTTPException(
            status_code=503,
            detail=(
                "국내주식 순위 API는 실전 환경에서 사용해야 합니다. "
                ".env의 KIS_MODE=real을 확인하세요."
            )
        )

    limit = max(1, min(int(limit), 30))

    token = get_access_token()

    url = (
        f"{KIS_BASE_URL}"
        "/uapi/domestic-stock/v1/quotations/volume-rank"
    )

    headers = make_kis_headers("FHPST01710000", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_COND_SCR_DIV_CODE": "20171",
        "FID_INPUT_ISCD": "0000",
        "FID_DIV_CLS_CODE": "0",
        "FID_BLNG_CLS_CODE": str(sort_code),
        "FID_TRGT_CLS_CODE": "111111111",
        "FID_TRGT_EXLS_CLS_CODE": "0000000000",
        "FID_INPUT_PRICE_1": "",
        "FID_INPUT_PRICE_2": "",
        "FID_VOL_CNT": "",
        "FID_INPUT_DATE_1": ""
    }

    data = kis_get_with_retry(
        url=url,
        headers=headers,
        params=params,
        max_retry=3
    )

    if data.get("rt_cd") != "0":
        raise HTTPException(
            status_code=502,
            detail={
                "message": "KIS 거래량 순위 요청 실패",
                "kis_response": data
            }
        )

    rows = parse_rank_rows(data)

    items = []

    for index, row in enumerate(rows[:limit], start=1):
        item = make_common_rank_item(row, index)

        if item is not None:
            items.append(item)

    return items


def get_kis_change_rate_rank(limit: int = 30) -> List[dict]:
    """
    KIS 국내주식 등락률 순위.
    상승률이 높은 종목부터 반환한다.
    """
    if KIS_MODE != "real":
        raise HTTPException(
            status_code=503,
            detail="등락률 순위 API는 KIS 실전 환경에서 사용해야 합니다."
        )

    limit = max(1, min(int(limit), 30))

    token = get_access_token()

    url = (
        f"{KIS_BASE_URL}"
        "/uapi/domestic-stock/v1/ranking/fluctuation"
    )

    headers = make_kis_headers("FHPST01700000", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_COND_SCR_DIV_CODE": "20170",
        "FID_INPUT_ISCD": "0000",

        # 0: 상승률 순위
        "FID_RANK_SORT_CLS_CODE": "0",

        "FID_INPUT_CNT_1": "0",
        "FID_PRC_CLS_CODE": "0",
        "FID_INPUT_PRICE_1": "",
        "FID_INPUT_PRICE_2": "",
        "FID_VOL_CNT": "",
        "FID_TRGT_CLS_CODE": "0",
        "FID_TRGT_EXLS_CLS_CODE": "0",
        "FID_DIV_CLS_CODE": "0",
        "FID_RSFL_RATE1": "",
        "FID_RSFL_RATE2": ""
    }

    data = kis_get_with_retry(
        url=url,
        headers=headers,
        params=params,
        max_retry=3
    )

    if data.get("rt_cd") != "0":
        raise HTTPException(
            status_code=502,
            detail={
                "message": "KIS 등락률 순위 요청 실패",
                "kis_response": data
            }
        )

    rows = parse_rank_rows(data)

    items = []

    for row in rows:
        item = make_common_rank_item(row, 0)

        if item is not None:
            items.append(item)

    # API 결과 정렬이 일정하지 않은 경우를 대비해 서버에서 다시 정렬한다.
    items.sort(
        key=lambda item: item.get("change_rate", 0.0),
        reverse=True
    )

    items = items[:limit]

    for index, item in enumerate(items, start=1):
        item["rank"] = index

    return items


def get_kis_market_cap_rank(limit: int = 30) -> List[dict]:
    """
    KIS 국내주식 시가총액 상위.
    """
    if KIS_MODE != "real":
        raise HTTPException(
            status_code=503,
            detail="시가총액 순위 API는 KIS 실전 환경에서 사용해야 합니다."
        )

    limit = max(1, min(int(limit), 30))

    token = get_access_token()

    url = (
        f"{KIS_BASE_URL}"
        "/uapi/domestic-stock/v1/ranking/market-cap"
    )

    headers = make_kis_headers("FHPST01740000", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_COND_SCR_DIV_CODE": "20174",
        "FID_INPUT_ISCD": "0000",
        "FID_DIV_CLS_CODE": "0",
        "FID_TRGT_CLS_CODE": "0",
        "FID_TRGT_EXLS_CLS_CODE": "0",
        "FID_INPUT_PRICE_1": "",
        "FID_INPUT_PRICE_2": "",
        "FID_VOL_CNT": ""
    }

    data = kis_get_with_retry(
        url=url,
        headers=headers,
        params=params,
        max_retry=3
    )

    if data.get("rt_cd") != "0":
        raise HTTPException(
            status_code=502,
            detail={
                "message": "KIS 시가총액 순위 요청 실패",
                "kis_response": data
            }
        )

    rows = parse_rank_rows(data)

    items = []

    for row in rows:
        item = make_common_rank_item(row, 0)

        if item is not None:
            items.append(item)

    items.sort(
        key=lambda item: item.get("market_cap", 0),
        reverse=True
    )

    items = items[:limit]

    for index, item in enumerate(items, start=1):
        item["rank"] = index

    return items


def get_kis_execution_strength_rank(limit: int = 30) -> List[dict]:
    """
    체결강도 순위.

    KIS 거래량순위 API에서 체결강도 정렬 코드 4를 먼저 시도한다.
    코드 4가 지원되지 않는 환경에서는 거래량 상위 30개 응답을
    체결강도(tday_rltv) 기준으로 서버에서 재정렬한다.
    """
    limit = max(1, min(int(limit), 30))

    try:
        items = request_kis_volume_rank(
            sort_code="4",
            limit=limit
        )

        valid_strength_count = sum(
            1
            for item in items
            if item.get("execution_strength", 0.0) > 0
        )

        if valid_strength_count > 0:
            items.sort(
                key=lambda item: item.get("execution_strength", 0.0),
                reverse=True
            )

            for index, item in enumerate(items, start=1):
                item["rank"] = index

            return items

    except Exception as e:
        print("[EXECUTION STRENGTH SORT CODE 4 FAIL]", e)

    # 정렬 코드 4가 지원되지 않을 때 거래량 상위 데이터 재정렬
    items = request_kis_volume_rank(
        sort_code="0",
        limit=30
    )

    items.sort(
        key=lambda item: item.get("execution_strength", 0.0),
        reverse=True
    )

    items = items[:limit]

    for index, item in enumerate(items, start=1):
        item["rank"] = index

    return items


def get_kis_stock_rank(
    rank_type: str,
    limit: int = 30
) -> List[dict]:
    rank_type = rank_type.strip().lower()
    limit = max(1, min(int(limit), 30))

    if rank_type == RANK_TYPE_VOLUME:
        return request_kis_volume_rank(
            sort_code="0",
            limit=limit
        )

    if rank_type == RANK_TYPE_CHANGE_RATE:
        return get_kis_change_rate_rank(limit)

    if rank_type == RANK_TYPE_MARKET_CAP:
        return get_kis_market_cap_rank(limit)

    if rank_type == RANK_TYPE_EXECUTION_STRENGTH:
        return get_kis_execution_strength_rank(limit)

    raise HTTPException(
        status_code=400,
        detail=(
            "지원하지 않는 순위 종류입니다. "
            "volume, change-rate, market-cap, "
            "execution-strength 중 하나를 사용하세요."
        )
    )

def get_kis_volume_rank(limit: int = 30) -> List[dict]:
    """
    KIS 국내주식 거래량순위 조회.

    - 실전 KIS API 사용
    - KRX 국내주식 조회
    - 누적 거래량 기준 정렬
    - 결과를 MongoDB에 저장하지 않음
    """

    if KIS_MODE != "real":
        raise HTTPException(
            status_code=503,
            detail=(
                "KIS 거래량순위 API는 실전 환경에서만 지원됩니다. "
                ".env의 KIS_MODE=real 및 실전 App Key/Secret을 확인하세요."
            )
        )

    limit = max(1, min(int(limit), 30))
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/volume-rank"
    headers = make_kis_headers("FHPST01710000", token)

    params = {
        # 공식 KIS 거래량순위 검사 예제와 동일한 조합
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_COND_SCR_DIV_CODE": "20171",
        "FID_INPUT_ISCD": "0002",
        "FID_DIV_CLS_CODE": "0",
        "FID_BLNG_CLS_CODE": "0",
        "FID_TRGT_CLS_CODE": "111111111",
        "FID_TRGT_EXLS_CLS_CODE": "000000",
        "FID_INPUT_PRICE_1": "0",
        "FID_INPUT_PRICE_2": "0",
        "FID_VOL_CNT": "0",
        "FID_INPUT_DATE_1": "0"
    }

    print(
        "[KIS VOLUME RANK REQUEST]",
        {
            "patch": PATCH_VERSION,
            "mode": KIS_MODE,
            "url": url,
            "market": params["FID_COND_MRKT_DIV_CODE"],
            "screen": params["FID_COND_SCR_DIV_CODE"],
            "input_iscd": params["FID_INPUT_ISCD"],
            "sort": params["FID_BLNG_CLS_CODE"]
        }
    )

    data = kis_get_with_retry(
        url=url,
        headers=headers,
        params=params,
        max_retry=3,
        timeout_sec=8
    )

    if data.get("rt_cd") != "0":
        print(
            "[KIS VOLUME RANK ERROR]",
            json.dumps(data, ensure_ascii=False, indent=2)
        )

        raise HTTPException(
            status_code=502,
            detail={
                "msg_cd": data.get("msg_cd"),
                "msg1": data.get("msg1"),
                "request_market": params["FID_COND_MRKT_DIV_CODE"],
                "request_input_iscd": params["FID_INPUT_ISCD"],
                "patch_version": PATCH_VERSION
            }
        )

    output = data.get("output", [])

    if isinstance(output, dict):
        output = [output]

    if not isinstance(output, list):
        output = []

    items: List[dict] = []

    for row in output:
        symbol = str(
            row.get("mksc_shrn_iscd")
            or row.get("stck_shrn_iscd")
            or ""
        ).strip()

        if len(symbol) != 6 or not symbol.isdigit():
            continue

        name = str(
            row.get("hts_kor_isnm")
            or row.get("prdt_name")
            or symbol
        ).strip()

        price = parse_int(row.get("stck_prpr"))
        current_volume = parse_int(row.get("acml_vol"))
        previous_volume = parse_int(row.get("prdy_vol"))

        # 오전 9시 전에는 오늘 거래량이 0이므로 전일 거래량 사용
        before_market_open = datetime.now().time() < dt_time(9, 0)

        if before_market_open:
            volume = previous_volume
        else:
            volume = current_volume

        trade_value = parse_int(row.get("acml_tr_pbmn"))

        latest_names[symbol] = name

        if price > 0:
            latest_prices[symbol] = price

        # MST에 없는 예외 종목만 보완한다. 기존 KOSPI/KOSDAQ 정보는 덮어쓰지 않는다.
        if symbol not in stock_master:
            stock_master[symbol] = {
                "symbol": symbol,
                "name": name,
                "market": "KIS_KRX",
                "standard_code": "",
                "security_group": ""
            }

        items.append({
            "symbol": symbol,
            "name": name,
            "price": price,
            "volume": volume,
            "trade_value": trade_value,
            "rank": 0
        })

    # API 순서를 그대로 사용해도 되지만 방어적으로 누적 거래량 내림차순 정렬
    items.sort(key=lambda item: item["volume"], reverse=True)
    items = items[:limit]

    for index, item in enumerate(items, start=1):
        item["rank"] = index

    print(
        f"[KIS VOLUME RANK SUCCESS] "
        f"count={len(items)}, mode={KIS_MODE}"
    )

    return items


class OrderBookRow(BaseModel):
    price: int
    quantity: int
    side: str  # ask / bid


class OrderBookExtraInfo(BaseModel):
    current_price: int = 0          # 현재 체결가
    previous_close: int = 0
    execution_strength: float = 0.0 # 체결강도

    week52_high: int = 0            # 52주 최고
    week52_low: int = 0             # 52주 최저

    upper_limit: int = 0            # 상한가
    lower_limit: int = 0            # 하한가

    open_price: int = 0             # 시가
    high_price: int = 0             # 고가
    low_price: int = 0              # 저가

    volume: int = 0                 # 거래량
    volume_vs_yesterday_rate: float = 0.0 # 전일 대비 거래량 비율


class OrderBookResponse(BaseModel):
    symbol: str
    rows: List[OrderBookRow]
    info: OrderBookExtraInfo


@app.get("/orderbook", response_model=OrderBookResponse)
def get_orderbook(symbol: str = Query("005930")):
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-asking-price-exp-ccn"
    headers = make_kis_headers("FHKST01010200", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "UN",
        "FID_INPUT_ISCD": symbol
    }

    data = kis_get_with_retry(url, headers, params, max_retry=3)

    if data.get("rt_cd") != "0":
        raise HTTPException(status_code=500, detail=data)

    output = data.get("output1", {})

    rows = []

    # 매도호가: 높은 가격부터 현재가 근처까지
    for i in range(10, 0, -1):
        price = parse_int(output.get(f"askp{i}"))
        qty = parse_int(output.get(f"askp_rsqn{i}"))

        if price > 0:
            rows.append({
                "price": price,
                "quantity": qty,
                "side": "ask"
            })

    # 매수호가: 현재가 근처부터 낮은 가격까지
    for i in range(1, 11):
        price = parse_int(output.get(f"bidp{i}"))
        qty = parse_int(output.get(f"bidp_rsqn{i}"))

        if price > 0:
            rows.append({
                "price": price,
                "quantity": qty,
                "side": "bid"
            })

    extra_info = get_korea_orderbook_extra_info(symbol)

    return {
        "symbol": symbol,
        "rows": rows,
        "info": extra_info
    }

def is_minute_cache_fresh(symbol: str, max_delay_minutes: int = 2) -> bool:
    last_doc = get_last_minute_candle_doc(symbol)

    if last_doc is None:
        return False

    last_time_key = last_doc.get("time_key")

    if not last_time_key:
        return False

    now_key = normalize_market_minute_end_time()

    try:
        last_dt = datetime.strptime(last_time_key, "%H%M%S")
        now_dt = datetime.strptime(now_key, "%H%M%S")
    except Exception:
        return False

    diff = now_dt - last_dt

    return diff <= timedelta(minutes=max_delay_minutes)

def get_krx_stock_name(symbol: str) -> str:
    symbol = normalize_symbol(symbol)

    # 1순위: 서버 시작 시 로드한 KRX 종목 마스터
    info = stock_master.get(symbol)

    if info is not None:
        name = info.get("name")

        if name:
            return name

    # 2순위: MongoDB에 저장된 KRX 일별 데이터
    doc = krx_daily_collection.find_one(
        {"symbol": symbol},
        sort=[("date", -1)]
    )

    if doc is not None:
        name = doc.get("name")

        if name:
            return name

    # 실패하면 종목코드 반환
    return symbol

@app.get("/stock-detail")
def stock_detail(symbol: str = Query("005930")):
    symbol = normalize_symbol(symbol)

    extra = get_korea_orderbook_extra_info(symbol)
    ratio = get_korea_financial_ratio(symbol)
    income = get_korea_income_statement(symbol)
    investor = get_stock_investor_supply(symbol)

    return {
        "symbol": symbol,

        "open_price": extra.get("open_price", 0),
        "high_price": extra.get("high_price", 0),
        "low_price": extra.get("low_price", 0),
        "volume": extra.get("volume", 0),
        "volume_vs_yesterday_rate": extra.get("volume_vs_yesterday_rate", 0.0),

        "week52_high": extra.get("week52_high", 0),
        "week52_low": extra.get("week52_low", 0),
        "upper_limit": extra.get("upper_limit", 0),
        "lower_limit": extra.get("lower_limit", 0),

        "market_cap": extra.get("market_cap", 0),

        # 현재가 API에 있으면 그 값 우선, 없으면 재무비율 API 값 사용
        "per": extra.get("per", 0.0) or ratio.get("per", 0.0),
        "pbr": extra.get("pbr", 0.0) or ratio.get("pbr", 0.0),
        "eps": extra.get("eps", 0) or ratio.get("eps", 0),
        "bps": extra.get("bps", 0) or ratio.get("bps", 0),
        "roe": ratio.get("roe", 0.0),

        "revenue": income.get("revenue", 0),
        "operating_profit": income.get("operating_profit", 0),

        "individual_supply": investor.get("individual_supply", "중립"),
        "foreign_supply": investor.get("foreign_supply", "중립"),
        "institution_supply": investor.get("institution_supply", "중립")
    }

def judge_supply(value: int, threshold: int = 0) -> str:
    if value > threshold:
        return "매수 우위"

    if value < -threshold:
        return "매도 우위"

    return "중립"

def get_stock_investor_supply(symbol: str) -> dict:
    """
    주식현재가 투자자
    KIS 문서 메뉴: [국내주식] 기본시세 > 주식현재가 투자자
    """
    symbol = normalize_symbol(symbol)
    token = get_access_token()

    url = f"{KIS_BASE_URL}/uapi/domestic-stock/v1/quotations/inquire-investor"
    headers = make_kis_headers("FHKST01010900", token)

    params = {
        "FID_COND_MRKT_DIV_CODE": "J",
        "FID_INPUT_ISCD": symbol
    }

    try:
        data = kis_get_with_retry(url, headers, params, max_retry=2)

        if data.get("rt_cd") != "0":
            print("[INVESTOR SUPPLY ERROR]", json.dumps(data, ensure_ascii=False, indent=2))
            return {
                "individual_supply": "중립",
                "foreign_supply": "중립",
                "institution_supply": "중립"
            }

        print("[INVESTOR SUPPLY RAW]", json.dumps(data, ensure_ascii=False, indent=2))

        output = data.get("output", {})

        if isinstance(output, list):
            output = output[0] if output else {}

        # KIS 응답 필드명이 문서/시점마다 다를 수 있어서 후보를 넓게 둠
        individual_net = parse_int(
            output.get("prsn_ntby_qty")
            or output.get("prsn_ntby_tr_pbmn")
            or output.get("individual_net_buy")
            or output.get("indv_ntby_qty")
        )

        foreign_net = parse_int(
            output.get("frgn_ntby_qty")
            or output.get("frgn_ntby_tr_pbmn")
            or output.get("foreign_net_buy")
            or output.get("frgn_ntby_vol")
        )

        institution_net = parse_int(
            output.get("orgn_ntby_qty")
            or output.get("orgn_ntby_tr_pbmn")
            or output.get("institution_net_buy")
            or output.get("inst_ntby_qty")
        )

        return {
            "individual_supply": judge_supply(individual_net),
            "foreign_supply": judge_supply(foreign_net),
            "institution_supply": judge_supply(institution_net)
        }

    except Exception as e:
        print("[INVESTOR SUPPLY FAIL]", e)

        return {
            "individual_supply": "중립",
            "foreign_supply": "중립",
            "institution_supply": "중립"
        }

# =========================
# 네이버 종목 뉴스
# =========================

def clean_news_text(
    value: Optional[str]
) -> str:
    """
    네이버 검색 결과의 <b> 태그와
    HTML 특수문자를 제거한다.
    """
    if not value:
        return ""

    text = html.unescape(
        str(value)
    )

    text = re.sub(
        r"<[^>]+>",
        "",
        text
    )

    text = re.sub(
        r"\s+",
        " ",
        text
    )

    return text.strip()


def normalize_news_url(
    value: Optional[str]
) -> str:
    if not value:
        return ""

    return html.unescape(
        str(value)
    ).strip()


def get_news_source_name(
    original_link: str,
    naver_link: str
) -> str:
    """
    네이버 뉴스 검색 API에는 언론사 이름 필드가 없으므로
    원문 링크의 도메인을 기준으로 언론사명을 변환한다.
    """
    source_url = (
        original_link
        or naver_link
        or ""
    )

    try:
        domain = (
            urlparse(source_url)
            .netloc.lower()
            .strip()
        )
    except Exception:
        return "뉴스"

    if domain.startswith("www."):
        domain = domain[4:]

    source_mapping = {
        "yna.co.kr": "연합뉴스",
        "newsis.com": "뉴시스",
        "news1.kr": "뉴스1",
        "hankyung.com": "한국경제",
        "mk.co.kr": "매일경제",
        "sedaily.com": "서울경제",
        "edaily.co.kr": "이데일리",
        "fnnews.com": "파이낸셜뉴스",
        "mt.co.kr": "머니투데이",
        "asiae.co.kr": "아시아경제",
        "heraldcorp.com": "헤럴드경제",
        "etnews.com": "전자신문",
        "zdnet.co.kr": "지디넷코리아",
        "thebell.co.kr": "더벨",
        "businesspost.co.kr": "비즈니스포스트",
        "chosun.com": "조선일보",
        "joongang.co.kr": "중앙일보",
        "donga.com": "동아일보",
        "khan.co.kr": "경향신문",
        "hani.co.kr": "한겨레",
        "ytn.co.kr": "YTN",
        "sbs.co.kr": "SBS",
        "kbs.co.kr": "KBS",
        "imbc.com": "MBC",
        "jtbc.co.kr": "JTBC",
        "digitaltoday.co.kr": "디지털투데이",
        "dealsite.co.kr": "딜사이트",
        "bloter.net": "블로터",
        "tokenpost.kr": "토큰포스트"
    }

    for key, source_name in source_mapping.items():
        if (
            domain == key
            or domain.endswith("." + key)
        ):
            return source_name

    if domain:
        return domain

    return "뉴스"


def format_news_date(
    raw_date: Optional[str]
) -> str:
    """
    네이버 pubDate:
    Sun, 27 Jul 2026 10:15:00 +0900

    Unity 표시:
    2026.07.27 10:15
    """
    if not raw_date:
        return ""

    try:
        parsed_date = parsedate_to_datetime(
            str(raw_date)
        )

        return parsed_date.strftime(
            "%Y.%m.%d %H:%M"
        )

    except Exception:
        return str(raw_date)


def make_news_id(
    original_link: str,
    naver_link: str,
    title: str,
    published_at: str
) -> str:
    """
    Unity에서 중복 기사를 제거하기 위한 식별값.
    """
    source_url = (
        original_link
        or naver_link
        or ""
    )

    if source_url:
        return source_url

    return (
        f"{title}|"
        f"{published_at}"
    )


def get_stock_name_for_news(
    symbol: str
) -> str:
    """
    종목코드를 KIS MST 종목명으로 변환한다.
    """
    symbol = normalize_symbol(symbol)

    stock_info = stock_master.get(
        symbol,
        {}
    )

    stock_name = str(
        stock_info.get("name")
        or latest_names.get(symbol)
        or ""
    ).strip()

    if not stock_name:
        raise HTTPException(
            status_code=404,
            detail=(
                f"종목명을 찾을 수 없습니다: "
                f"{symbol}"
            )
        )

    return stock_name


def get_stock_news_from_naver(
    symbol: str,
    start: int,
    display: int
) -> dict:
    """
    현재 선택한 종목의 네이버 뉴스를 조회한다.

    최초:
    start=1, display=30

    추가:
    start=31, display=30
    start=61, display=30
    """
    symbol = normalize_symbol(symbol)

    if (
        not NAVER_CLIENT_ID
        or not NAVER_CLIENT_SECRET
    ):
        raise HTTPException(
            status_code=500,
            detail=(
                "NAVER_CLIENT_ID 또는 "
                "NAVER_CLIENT_SECRET이 설정되지 않았습니다."
            )
        )

    stock_name = get_stock_name_for_news(
        symbol
    )

    request_start = max(
        1,
        min(int(start), 1000)
    )

    request_display = max(
        1,
        min(int(display), 100)
    )

    headers = {
        "X-Naver-Client-Id":
            NAVER_CLIENT_ID,

        "X-Naver-Client-Secret":
            NAVER_CLIENT_SECRET
    }

    params = {
        "query": stock_name,
        "display": request_display,
        "start": request_start,
        "sort": "date"
    }

    print(
        "[NAVER STOCK NEWS REQUEST]",
        {
            "symbol": symbol,
            "name": stock_name,
            "start": request_start,
            "display": request_display
        }
    )

    try:
        response = requests.get(
            NAVER_NEWS_URL,
            headers=headers,
            params=params,
            timeout=10
        )

    except requests.RequestException as error:
        raise HTTPException(
            status_code=502,
            detail=(
                "네이버 뉴스 요청 실패: "
                f"{error}"
            )
        )

    if response.status_code != 200:
        print(
            "[NAVER NEWS ERROR]",
            response.status_code,
            response.text
        )

        raise HTTPException(
            status_code=502,
            detail={
                "message":
                    "네이버 뉴스 API 오류",

                "status":
                    response.status_code,

                "response":
                    response.text
            }
        )

    try:
        data = response.json()

    except ValueError:
        raise HTTPException(
            status_code=502,
            detail=(
                "네이버 뉴스 응답이 "
                "JSON 형식이 아닙니다."
            )
        )

    raw_items = data.get(
        "items",
        []
    )

    if not isinstance(raw_items, list):
        raw_items = []

    items = []

    for row in raw_items:
        title = clean_news_text(
            row.get("title")
        )

        description = clean_news_text(
            row.get("description")
        )

        original_link = normalize_news_url(
            row.get("originallink")
        )

        naver_link = normalize_news_url(
            row.get("link")
        )

        published_at = format_news_date(
            row.get("pubDate")
        )

        source_name = get_news_source_name(
            original_link=original_link,
            naver_link=naver_link
        )

        open_url = (
                original_link
                or naver_link
        )

        if not title or not open_url:
            continue

        items.append({
            "id": make_news_id(
                original_link=original_link,
                naver_link=naver_link,
                title=title,
                published_at=published_at
            ),
            "source": source_name,
            "published_at": published_at,
            "description": description,
            "link": open_url
        })

    total = int(
        data.get("total", 0)
        or 0
    )

    returned_start = int(
        data.get(
            "start",
            request_start
        )
        or request_start
    )

    returned_display = int(
        data.get(
            "display",
            len(raw_items)
        )
        or len(raw_items)
    )

    next_start = (
            returned_start
            + returned_display
    )

    has_more = (
            len(raw_items) >= request_display
            and next_start <= 1000
            and next_start <= total
    )

    return {
        "symbol": symbol,
        "name": stock_name,
        "total": total,
        "start": returned_start,
        "display": returned_display,
        "next_start": (
            next_start
            if has_more
            else None
        ),
        "has_more": has_more,
        "items": items
    }


 # Integrated application trading engine (no separate server/module required).
import asyncio
import logging
import os
import re
import uuid
from datetime import datetime, timezone, timedelta
from typing import Literal, Optional

import bcrypt
from bson import ObjectId
from fastapi import Depends, HTTPException
from pydantic import BaseModel, Field, StrictInt
from pymongo.errors import OperationFailure, DuplicateKeyError

log = logging.getLogger(__name__)


def utcnow():
    return datetime.now(timezone.utc)


def execution_allowed(now=None):
    if os.getenv("ALLOW_CLOSED_MARKET_ORDERS", "false").lower() == "true":
        return True
    now = (now or utcnow()).astimezone(timezone(timedelta(hours=9)))
    closed = {part.strip().split(":")[0] for part in
              os.getenv("KRX_CLOSED_DATES", "").split(",")}
    minutes = now.hour * 60 + now.minute
    return now.weekday() < 5 and now.date().isoformat() not in closed and 540 <= minutes < 930


def can_fill(side, order_type, price, limit_price, allowed):
    return allowed and (order_type == "MARKET" or
                       (price <= limit_price if side == "BUY" else price >= limit_price))


def public(doc):
    if doc is None:
        return None
    result = {}
    for key, value in doc.items():
        if isinstance(value, ObjectId):
            value = str(value)
        elif isinstance(value, datetime):
            value = value.replace(tzinfo=timezone.utc) if value.tzinfo is None else value
            value = value.isoformat()
        result[key] = value
    return result


class OrderInput(BaseModel):
    symbol: str
    side: Literal["BUY", "SELL"]
    orderType: Literal["MARKET", "LIMIT"]
    quantity: StrictInt = Field(gt=0, le=1_000_000)
    limitPrice: Optional[StrictInt] = Field(default=None, gt=0, le=1_000_000_000)
    clientRequestId: str = Field(min_length=8, max_length=100)


class AmendInput(BaseModel):
    quantity: StrictInt = Field(gt=0, le=1_000_000)
    limitPrice: StrictInt = Field(gt=0, le=1_000_000_000)
    expectedVersion: StrictInt = Field(ge=0)
    clientRequestId: str = Field(min_length=8, max_length=100)


class CancelInput(BaseModel):
    expectedVersion: Optional[StrictInt] = Field(default=None, ge=0)


class TopUpInput(BaseModel):
    clientRequestId: str = Field(min_length=8, max_length=100)


class LegacyOrderInput(BaseModel):
    symbol: str
    quantity: StrictInt = Field(gt=0, le=1_000_000)
    order_type: Literal["MARKET", "LIMIT"] = "MARKET"
    limit_price: Optional[StrictInt] = Field(default=None, gt=0, le=1_000_000_000)
    client_request_id: Optional[str] = Field(default=None, min_length=8, max_length=100)


class Credentials(BaseModel):
    username: str = Field(min_length=3, max_length=30)
    password: str = Field(min_length=6, max_length=100)


class TradingStore:
    def __init__(self, db, quote):
        self.db = db
        self.quote = quote
        self.accounts = db["tradingaccounts"]
        self.holdings = db["holdings"]
        self.orders = db["tradeorders"]
        self.users = db["users"]
        self.funding = db["fundingtransactions"]

    def initialize(self):
        hello = self.db.client.admin.command("hello")
        if not hello.get("setName") and hello.get("msg") != "isdbgrid":
            raise RuntimeError("거래 서버는 Replica Set MongoDB가 필요합니다. 안내서의 로컬 설정을 적용하세요.")
        self.accounts.create_index("userId", unique=True)
        self.holdings.create_index([("userId", 1), ("symbol", 1)], unique=True)
        self.orders.create_index([("userId", 1), ("createdAt", -1)])
        self.orders.create_index([("userId", 1), ("clientRequestId", 1)], unique=True,
                                 partialFilterExpression={"clientRequestId": {"$type": "string"}})
        self.users.create_index("username_normalized", unique=True,
            partialFilterExpression={"username_normalized": {"$type": "string"}},
            name="app_username_normalized_unique")
        self.funding.create_index([("userId", 1), ("appRequestId", 1)], unique=True,
            partialFilterExpression={"appRequestId": {"$type": "string"}})

    def transaction(self, callback):
        try:
            with self.db.client.start_session() as session:
                return session.with_transaction(callback)
        except OperationFailure as error:
            if error.code in (20, 303):
                raise HTTPException(503, "MongoDB Replica Set 설정이 필요합니다.") from error
            raise

    def account(self, uid, session):
        # This shared write serializes this server's mutations per account.
        self.accounts.update_one({"userId": uid}, {
            "$setOnInsert": {"userId": uid, "cash": 0, "reservedCash": 0,
                             "initialCash": 0, "totalDeposits": 0, "manualDeposits": 0,
                             "salaryPlanDeposits": 0, "salaryPlanFundingEnabled": False,
                             "salaryPlanFundingAmount": 0, "currency": "KRW", "createdAt": utcnow()},
            "$inc": {"appMutationVersion": 1}, "$set": {"updatedAt": utcnow()}
        }, upsert=True, session=session)
        return self.accounts.find_one({"userId": uid}, session=session)

    def fresh_quote(self, symbol):
        if not re.fullmatch(r"\d{6}", symbol):
            raise HTTPException(400, "종목 코드는 6자리 숫자여야 합니다.")
        result = self.quote(symbol)  # Provider call outside retryable transaction.
        price = int(result.price)
        if price <= 0:
            raise HTTPException(502, "현재가를 조회하지 못했습니다.")
        return {"price": price, "name": result.name or symbol,
                "market": result.market or "KRX"}

    def release(self, uid, order, account, holding):
        if order["side"] == "BUY":
            amount = order.get("reservedAmount", 0)
            if account.get("reservedCash", 0) < amount:
                raise HTTPException(409, "예약금 데이터가 일치하지 않습니다.")
            account["reservedCash"] -= amount
        else:
            quantity = order.get("reservedQuantity", 0)
            if not holding or holding.get("reservedQuantity", 0) < quantity:
                raise HTTPException(409, "예약수량 데이터가 일치하지 않습니다.")
            holding["reservedQuantity"] -= quantity
        order["reservedAmount"] = 0
        order["reservedQuantity"] = 0

    def apply(self, order, account, holding, quote, allowed):
        uid, symbol, quantity = order["userId"], order["symbol"], order["quantity"]
        limit = order.get("limitPrice")
        fill = can_fill(order["side"], order["orderType"], quote["price"], limit, allowed)
        price = quote["price"] if fill else limit
        if order["side"] == "BUY":
            amount = price * quantity
            if account["cash"] - account.get("reservedCash", 0) < amount:
                raise HTTPException(400, "주문 가능 현금이 부족합니다.")
            if fill:
                account["cash"] -= amount
                holding = holding or {"_id": ObjectId(), "userId": uid, "symbol": symbol,
                                      "quantity": 0, "reservedQuantity": 0, "avgPrice": 0,
                                      "createdAt": utcnow()}
                total = holding["quantity"] * holding["avgPrice"] + amount
                holding["quantity"] += quantity
                holding["avgPrice"] = total / holding["quantity"]
                holding["totalBuyAmount"] = total
                holding["name"], holding["market"] = order["name"], order["market"]
            else:
                account["reservedCash"] += amount
                order["reservedAmount"] = amount
        else:
            if not holding or holding["quantity"] - holding.get("reservedQuantity", 0) < quantity:
                raise HTTPException(400, "매도 가능 수량이 부족합니다.")
            if fill:
                account["cash"] += price * quantity
                order["realizedProfit"] = (price - holding["avgPrice"]) * quantity
                holding["quantity"] -= quantity
                holding["totalBuyAmount"] = holding["quantity"] * holding["avgPrice"]
            else:
                holding["reservedQuantity"] = holding.get("reservedQuantity", 0) + quantity
                order["reservedQuantity"] = quantity
        order["status"] = "FILLED" if fill else "PENDING"
        order["filledQuantity"] = quantity if fill else 0
        order["executedPrice"] = price if fill else None
        order["executedAt"] = utcnow() if fill else None
        order["updatedAt"] = utcnow()
        order["version"] = order.get("version", 0) + 1
        return holding

    def persist(self, account, holding, order, session):
        account["updatedAt"] = utcnow()
        self.accounts.replace_one({"_id": account["_id"]}, account, session=session)
        if holding:
            if holding["quantity"] == 0 and holding.get("reservedQuantity", 0) == 0:
                self.holdings.delete_one({"_id": holding["_id"]}, session=session)
            else:
                holding["updatedAt"] = utcnow()
                self.holdings.replace_one({"_id": holding["_id"]}, holding, upsert=True, session=session)
        self.orders.replace_one({"_id": order["_id"]}, order, upsert=True, session=session)
        return public(order)

    def create(self, uid, request):
        previous = self.orders.find_one({"userId": uid, "clientRequestId": request.clientRequestId})
        if previous:
            self.check_request(previous, request)
            return public(previous)
        if request.orderType == "LIMIT" and request.limitPrice is None:
            raise HTTPException(400, "지정가를 입력해 주세요.")
        allowed = execution_allowed()
        if request.orderType == "MARKET" and not allowed:
            raise HTTPException(409, "시장가 주문은 정규장 운영시간에만 가능합니다.")
        quote = self.fresh_quote(request.symbol)

        def work(session):
            account = self.account(uid, session)
            previous = self.orders.find_one({"userId": uid, "clientRequestId": request.clientRequestId}, session=session)
            if previous:
                self.check_request(previous, request)
                return public(previous)
            holding = self.holdings.find_one({"userId": uid, "symbol": request.symbol}, session=session)
            order = {"_id": ObjectId(), "userId": uid, "symbol": request.symbol,
                     "name": quote["name"], "market": quote["market"], "side": request.side,
                     "orderType": request.orderType, "quantity": request.quantity,
                     "limitPrice": request.limitPrice if request.orderType == "LIMIT" else None,
                     "orderPrice": request.limitPrice if request.orderType == "LIMIT" else quote["price"],
                     "reservedAmount": 0, "reservedQuantity": 0, "realizedProfit": 0,
                     "clientRequestId": request.clientRequestId, "createdAt": utcnow(),
                     "version": 0, "amendments": [], "origin": "UNITY"}
            order["originalRequest"] = {"symbol": request.symbol, "side": request.side,
                "orderType": request.orderType, "quantity": request.quantity,
                "limitPrice": order["limitPrice"]}
            holding = self.apply(order, account, holding, quote, allowed)
            return self.persist(account, holding, order, session)
        return self.transaction(work)

    @staticmethod
    def check_request(order, request):
        expected = (request.symbol, request.side, request.orderType, request.quantity,
                    request.limitPrice if request.orderType == "LIMIT" else None)
        original = order.get("originalRequest", order)
        actual = (original["symbol"], original["side"], original["orderType"], original["quantity"], original.get("limitPrice"))
        if actual != expected:
            raise HTTPException(409, "동일한 요청 ID에 다른 주문을 사용할 수 없습니다.")

    def get_order(self, uid, oid, session):
        if not ObjectId.is_valid(oid):
            raise HTTPException(400, "주문 ID가 올바르지 않습니다.")
        order = self.orders.find_one({"_id": ObjectId(oid), "userId": uid}, session=session)
        if not order:
            raise HTTPException(404, "주문을 찾을 수 없습니다.")
        return order

    def mutate(self, uid, oid, action, request=None, quote=None):
        # Quote collected before session; repeated commits do not re-fetch price.
        def work(session):
            account = self.account(uid, session)
            order = self.get_order(uid, oid, session)
            if action == "amend":
                for amendment in order.get("amendments", []):
                    if amendment["clientRequestId"] == request.clientRequestId:
                        if (amendment["newQuantity"], amendment["newLimitPrice"]) != (request.quantity, request.limitPrice):
                            raise HTTPException(409, "동일한 정정 요청 ID에 다른 내용을 사용할 수 없습니다.")
                        return public(order)
            if action == "cancel" and order["status"] == "CANCELED":
                return public(order)
            if order["status"] != "PENDING" or order.get("filledQuantity", 0) != 0:
                if action == "fill":
                    return public(order)
                raise HTTPException(409, "미체결 주문만 정정·취소할 수 있습니다. 새로고침하세요.")
            expected = getattr(request, "expectedVersion", None)
            if expected is not None and expected != order.get("version", 0):
                raise HTTPException(409, "주문이 변경되었습니다. 새로고침 후 다시 선택하세요.")
            holding = self.holdings.find_one({"userId": uid, "symbol": order["symbol"]}, session=session)
            if action == "fill" and not can_fill(order["side"], order["orderType"], quote["price"],
                                                order.get("limitPrice"), execution_allowed()):
                return public(order)
            self.release(uid, order, account, holding)
            if action == "cancel":
                order.update(status="CANCELED", canceledAt=utcnow(), updatedAt=utcnow(),
                             version=order.get("version", 0) + 1)
            else:
                if action == "amend":
                    if order["orderType"] != "LIMIT":
                        raise HTTPException(409, "지정가 주문만 정정할 수 있습니다.")
                    order.setdefault("amendments", []).append({"quantity": order["quantity"],
                        "limitPrice": order["limitPrice"], "newQuantity": request.quantity,
                        "newLimitPrice": request.limitPrice, "at": utcnow(),
                        "clientRequestId": request.clientRequestId})
                    order.update(quantity=request.quantity, limitPrice=request.limitPrice,
                                 orderPrice=request.limitPrice)
                holding = self.apply(order, account, holding, quote, execution_allowed())
            return self.persist(account, holding, order, session)
        return self.transaction(work)

    def portfolio(self, uid):
        def work(session):
            account = self.account(uid, session)
            holdings = list(self.holdings.find({"userId": uid}, session=session))
            return {"cash": account["cash"], "reservedCash": account.get("reservedCash", 0),
                    "availableCash": account["cash"] - account.get("reservedCash", 0),
                    "holdings": [dict(public(h), availableQuantity=h["quantity"] - h.get("reservedQuantity", 0),
                                      avg_price=h["avgPrice"]) for h in holdings]}
        return self.transaction(work)

    def check_pending(self, uid=None):
        if not execution_allowed():
            return []
        query = {"status": "PENDING", "orderType": "LIMIT"}
        if uid:
            query["userId"] = uid
        quotes, results = {}, []
        for order in self.orders.find(query).sort("createdAt", 1).limit(200):
            try:
                symbol = order["symbol"]
                if symbol not in quotes:
                    quotes[symbol] = self.fresh_quote(symbol)
                result = self.mutate(order["userId"], str(order["_id"]), "fill", quote=quotes[symbol])
                if result["status"] == "FILLED":
                    results.append(result)
            except HTTPException as error:
                log.warning("Pending order %s deferred: %s", order["_id"], error.detail)
            except Exception:
                log.exception("Pending order %s failed", order["_id"])
        return results


    def top_up(self, uid, request_id):
        def work(session):
            account = self.account(uid, session)
            if not self.funding.find_one({"userId": uid, "appRequestId": request_id}, session=session):
                self.funding.insert_one({"userId": uid, "market": "KR", "type": "MANUAL_TOP_UP",
                    "amount": 1_000_000, "currency": "KRW", "appRequestId": request_id,
                    "fundingKey": "unity-" + request_id, "createdAt": utcnow(), "updatedAt": utcnow()}, session=session)
                account["cash"] += 1_000_000
                account["totalDeposits"] = account.get("totalDeposits", 0) + 1_000_000
                account["manualDeposits"] = account.get("manualDeposits", 0) + 1_000_000
                self.accounts.replace_one({"_id": account["_id"]}, account, session=session)
            return {"amount": 1_000_000, "account": public(account)}
        return self.transaction(work)

app_trading = TradingStore(mongo_db, get_korea_best_price)

@app.get("/stock/news")
def stock_news(
        symbol: str = Query("005930"),
        start: int = Query(
            1,
            ge=1,
            le=1000
        ),
        display: int = Query(
            30,
            ge=1,
            le=100
        )
):
    """
    선택한 종목 뉴스 조회.

    예:
    /stock/news
        ?symbol=005930
        &start=1
        &display=30
    """
    return get_stock_news_from_naver(
        symbol=symbol,
        start=start,
        display=display
    )


# Existing /buy, /sell and /portfolio remain the Unity entry points.
# New routes power prefab order history, amendments and cancellations.
@app.get("/orders")
@app.get("/api/trading/orders")
def get_app_orders(
    symbol: Optional[str] = None,
    side: Optional[Literal["BUY", "SELL"]] = None,
    status: Optional[Literal["PENDING", "FILLED", "CANCELED", "REJECTED"]] = None,
    limit: int = Query(100, ge=1, le=200),
    user_id: str = Depends(get_current_user_id)
):
    query = {"userId": user_id}
    for key, value in (("symbol", symbol), ("side", side), ("status", status)):
        if value:
            query[key] = value
    return {"success": True, "data": [public(row) for row in
        app_trading.orders.find(query).sort("createdAt", -1).limit(limit)]}


@app.post("/orders/check-pending")
def check_app_pending(user_id: str = Depends(get_current_user_id)):
    return {"success": True, "data": app_trading.check_pending(user_id)}


@app.post("/orders/{order_id}/amend")
def amend_app_order(order_id: str, request: AmendInput,
                    user_id: str = Depends(get_current_user_id)):
    if not ObjectId.is_valid(order_id):
        raise HTTPException(400, "올바른 주문 ID가 필요합니다.")
    order = app_trading.orders.find_one({"_id": ObjectId(order_id), "userId": user_id})
    if not order:
        raise HTTPException(404, "주문을 찾을 수 없습니다.")
    quote = app_trading.fresh_quote(order["symbol"])
    return {"success": True, "data": app_trading.mutate(user_id, order_id, "amend", request, quote)}


@app.post("/orders/{order_id}/cancel")
def cancel_app_order(order_id: str, request: CancelInput,
                     user_id: str = Depends(get_current_user_id)):
    return {"success": True, "data": app_trading.mutate(user_id, order_id, "cancel", request)}


@app.post("/top-up")
def top_up_app_account(request: TopUpInput, user_id: str = Depends(get_current_user_id)):
    return {"success": True, "data": app_trading.top_up(user_id, request.clientRequestId)}


async def app_pending_order_loop():
    while True:
        try:
            await asyncio.to_thread(app_trading.check_pending)
        except Exception:
            log.exception("Application pending order check failed")
        await asyncio.sleep(5)


@app.on_event("startup")
async def start_app_order_scheduler():
    app.state.order_scheduler = asyncio.create_task(app_pending_order_loop())


@app.on_event("shutdown")
async def stop_app_order_scheduler():
    task = getattr(app.state, "order_scheduler", None)
    if task:
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass
