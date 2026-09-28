"""Unity에서 선택한 종목을 즉시 분석하는 API."""
import logging
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from services.ramen.auth import get_current_user_id
from services.ramen.config import settings
from services.ramen.marketdata.poller import build_tick
from services.ramen.models.schemas import Factor, JudgmentResponse
from services.ramen.news.newsapi_client import extract_text, fetch_articles
from services.ramen.pipeline import run_judgment_pipeline

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/judgment", tags=["judgment"])


class AnalyzeRequest(BaseModel):
    symbol: str = Field(min_length=1, max_length=20)
    stock_name: str | None = Field(default=None, max_length=100)
    articles: list[str] | None = None


@router.post("/analyze", response_model=JudgmentResponse)
async def analyze_judgment(
    payload: AnalyzeRequest,
    user_id: str = Depends(get_current_user_id),
):
    symbol = payload.symbol.strip()
    if not symbol:
        raise HTTPException(status_code=422, detail="종목코드가 필요합니다")

    if not settings.kis_app_key or not settings.kis_app_secret:
        raise HTTPException(
            status_code=503,
            detail="AI 서버의 KIS_APP_KEY/KIS_APP_SECRET 설정이 필요합니다",
        )

    try:
        market_data = await build_tick(symbol)
    except Exception as error:
        logger.exception("종목 시세 수집 실패: %s", symbol)
        raise HTTPException(
            status_code=502,
            detail=f"종목 시세를 가져오지 못했습니다: {error}",
        ) from error

    articles = payload.articles or []
    if not articles and settings.newsapi_api_key:
        query = (payload.stock_name or symbol).strip()
        try:
            news_items = await fetch_articles(query, page_size=5)
            articles = [extract_text(item) for item in news_items]
            articles = [text for text in articles if text]
        except Exception:
            articles = []

    try:
        doc = await run_judgment_pipeline(
            symbol,
            market_data,
            articles=articles,
            user_id=user_id,
        )
    except Exception as error:
        logger.exception("AI 판단 파이프라인 실패: user=%s symbol=%s", user_id, symbol)
        raise HTTPException(
            status_code=502,
            detail=f"AI 판단을 생성하지 못했습니다: {error}",
        ) from error

    return JudgmentResponse(
        symbol=doc["symbol"],
        judge=doc["judge"],
        confidence=doc["confidence"],
        summary=doc["reason"],
        factors=[Factor(**factor) for factor in doc["factors"]],
        computed_at=doc["time"],
    )
