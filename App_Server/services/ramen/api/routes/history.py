from fastapi import APIRouter, Depends
from services.ramen.auth import get_current_user_id
from services.ramen.history.repository import get_history
from services.ramen.models.schemas import HistoryEntry

router = APIRouter(prefix="/judgment", tags=["judgment"])


@router.get("/{symbol}/history", response_model=list[HistoryEntry])
async def get_judgment_history(
    symbol: str,
    limit: int = 20,
    user_id: str = Depends(get_current_user_id),
):
    docs = await get_history(symbol, user_id, limit)
    return [
        HistoryEntry(
            time=doc["time"],
            confidence=doc.get("confidence", 0.0),
            judge=doc["judge"],
            reason=doc["reason"],
            changed=doc["changed"],
        )
        for doc in docs
    ]
