from fastapi import APIRouter, Depends, Query
from services.ramen.auth import get_current_user_id
from services.ramen.history.repository import get_history
from services.ramen.models.schemas import HistoryEntry, empty_probabilities

router = APIRouter(prefix="/judgment", tags=["judgment"])


@router.get("/{symbol}/history", response_model=list[HistoryEntry])
async def get_judgment_history(
    symbol: str, limit: int = Query(default=20, ge=1, le=100),
    user_id: str = Depends(get_current_user_id),
):
    docs = await get_history(symbol, user_id, limit)
    return [HistoryEntry(
        time=d["time"], judge=d["judge"], confidence=d.get("confidence", 0),
        probabilities=d.get("probabilities") or empty_probabilities(),
        reason=d["reason"], changed=d["changed"],
    ) for d in docs]
