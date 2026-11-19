"""기존 Unity 분석 버튼을 새 판단 파이프라인으로 연결한다."""
import logging
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from services.ramen.auth import get_current_user_id
from services.ramen.config import settings
from services.ramen.marketdata.poller import build_tick
from services.ramen.models.schemas import JudgmentResponse
from services.ramen.pipeline import run_judgment_pipeline
from services.ramen.tracking.registry import analysis_lock
from services.ramen.triggers.event_listener import mark_run, seed_state

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/judgment", tags=["judgment"])


class AnalyzeRequest(BaseModel):
    symbol: str = Field(min_length=1, max_length=20)
    stock_name: str | None = Field(default=None, max_length=100)
    # 예전 앱의 요청은 받되 새 버전은 뉴스 대신 기술적 지표를 계산한다.
    articles: list[str] | None = None


@router.post("/analyze", response_model=JudgmentResponse)
async def analyze_judgment(payload: AnalyzeRequest, user_id: str = Depends(get_current_user_id)):
    symbol = payload.symbol.strip()
    if not symbol:
        raise HTTPException(status_code=422, detail="종목코드가 필요합니다")
    if not settings.kis_app_key or not settings.kis_app_secret:
        raise HTTPException(status_code=503, detail="AI 서버의 KIS_APP_KEY/KIS_APP_SECRET 설정이 필요합니다")

    async with analysis_lock(symbol, user_id):
        try:
            tick = await build_tick(symbol)
        except Exception as error:
            logger.exception("종목 시세 수집 실패: %s", symbol)
            raise HTTPException(status_code=502, detail=f"종목 시세를 가져오지 못했습니다: {error}") from error
        try:
            doc = await run_judgment_pipeline(symbol, tick, user_id=user_id)
        except Exception as error:
            logger.exception("AI 판단 실행 실패: %s", symbol)
            raise HTTPException(status_code=502, detail=f"AI 판단을 생성하지 못했습니다: {error}") from error
        seed_state(symbol, tick, user_id)
        mark_run(symbol, user_id)
        return JudgmentResponse.from_document(doc)
