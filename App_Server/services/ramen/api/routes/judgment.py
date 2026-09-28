from fastapi import APIRouter, Depends, HTTPException
from services.ramen.auth import get_current_user_id
from services.ramen.history.repository import get_latest_judgment
from services.ramen.models.schemas import JudgmentResponse, Factor

router = APIRouter(prefix="/judgment", tags=["judgment"])


@router.get("/{symbol}", response_model=JudgmentResponse)
async def get_judgment(
    symbol: str,
    user_id: str = Depends(get_current_user_id),
):
    doc = await get_latest_judgment(symbol, user_id)
    if doc is None:
        raise HTTPException(status_code=404, detail="아직 계산된 판단이 없습니다")
    return JudgmentResponse(
        symbol=symbol,
        judge=doc["judge"],
        confidence=doc["confidence"],
        summary=doc["reason"],
        factors=[Factor(**factor) for factor in doc["factors"]],
        computed_at=doc["time"],
    )
