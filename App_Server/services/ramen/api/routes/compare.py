from fastapi import APIRouter, Depends, HTTPException
from services.ramen.auth import get_current_user_id
from services.ramen.history.repository import get_latest_judgment
from services.ramen.narrative.generator import generate_comparison
from services.ramen.models.schemas import CompareRequest, CompareResponse

router = APIRouter(prefix="/judgment", tags=["judgment"])


@router.post("/compare", response_model=CompareResponse)
async def compare_judgment(
    payload: CompareRequest,
    user_id: str = Depends(get_current_user_id),
):
    doc = await get_latest_judgment(payload.symbol, user_id)
    if doc is None:
        raise HTTPException(status_code=404, detail="비교할 AI 판단이 없습니다")

    explanation = await generate_comparison(payload.user_judge, doc["judge"], doc["factors"])
    highlighted: dict[str, list[str]] = {"직접요인": [], "간접요인": []}
    for factor in doc["factors"]:
        key = "직접요인" if factor["type"] == "직접" else "간접요인"
        highlighted[key].append(factor["factor"])

    return CompareResponse(
        user_judge=payload.user_judge,
        ai_judge=doc["judge"],
        explanation=explanation,
        highlighted_factors=highlighted,
    )
