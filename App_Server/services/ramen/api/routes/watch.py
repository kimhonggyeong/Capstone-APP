import logging
from fastapi import APIRouter, Depends, HTTPException
from services.ramen.auth import get_current_user_id
from services.ramen.config import settings
from services.ramen.marketdata.cold_start import cold_start
from services.ramen.models.schemas import empty_probabilities
from services.ramen.tracking.registry import unsubscribe

router = APIRouter(prefix="/judgment", tags=["judgment"])
logger = logging.getLogger(__name__)


@router.post("/{symbol}/watch")
async def watch_symbol(symbol: str, user_id: str = Depends(get_current_user_id)):
    symbol = symbol.strip()
    if not symbol or len(symbol) > 20:
        raise HTTPException(status_code=422, detail="올바른 종목코드가 필요합니다")
    if not settings.kis_app_key or not settings.kis_app_secret:
        raise HTTPException(status_code=503, detail="AI 서버의 KIS_APP_KEY/KIS_APP_SECRET 설정이 필요합니다")
    try:
        doc = await cold_start(symbol, user_id)
    except Exception as error:
        logger.exception("AI 종목 감시 시작 실패: %s", symbol)
        raise HTTPException(status_code=502, detail=f"AI 판단을 준비하지 못했습니다: {error}") from error
    return {"symbol": symbol, "judge": doc["judge"],
            "probabilities": doc.get("probabilities") or empty_probabilities()}


@router.delete("/{symbol}/watch")
async def unwatch_symbol(symbol: str, user_id: str = Depends(get_current_user_id)):
    unsubscribe(symbol.strip(), user_id)
    return {"symbol": symbol.strip(), "watching": False}
