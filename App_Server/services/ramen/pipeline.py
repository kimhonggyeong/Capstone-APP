"""전체 파이프라인 조립: 요인 조회 -> 스코어링 -> LLM 서술 -> 이력 저장."""
import asyncio
from datetime import datetime
from services.ramen.factors.resolver import resolve_factors
from services.ramen.factors.news_classifier import classify_news
from services.ramen.scoring.rubric import compute_weights
from services.ramen.scoring.judge import decide_judgment
from services.ramen.scoring.confidence import compute_confidence
from services.ramen.narrative.generator import generate_reasoning_summary
from services.ramen.history.repository import get_latest_judgment, save_judgment
from services.ramen.history.change_detector import detect_change


async def run_judgment_pipeline(
    symbol: str,
    market_data: dict,
    articles: list[str] | None = None,
    user_id: str = "system",
) -> dict:
    classified_news = [
        await asyncio.to_thread(classify_news, article) for article in (articles or [])
    ]
    resolved = resolve_factors(market_data, classified_news)
    weighted = compute_weights(resolved)
    judge = decide_judgment(weighted)
    confidence = compute_confidence(weighted)
    summary = await generate_reasoning_summary(judge, weighted)

    previous = await get_latest_judgment(symbol, user_id)
    changed = detect_change(previous, judge)
    doc = {
        "user_id": user_id,
        "symbol": symbol,
        "time": datetime.utcnow().isoformat(timespec="seconds"),
        "judge": judge,
        "confidence": confidence,
        "reason": summary,
        "factors": weighted,
        "changed": changed,
    }
    await save_judgment(doc)
    return doc
