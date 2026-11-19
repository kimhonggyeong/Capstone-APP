"""구독 시작 시 분석하고, 이후 하트비트에서는 저장된 결과를 재사용한다."""
import time

from services.ramen.config import settings
from services.ramen.history.repository import get_latest_judgment
from services.ramen.marketdata.poller import build_tick
from services.ramen.pipeline import run_judgment_pipeline
from services.ramen.tracking.registry import analysis_lock, is_subscribed, subscribe
from services.ramen.triggers.event_listener import cooldown_active, mark_run, seed_state

_last_freshness_check: dict[tuple[str, str], float] = {}


async def cold_start(symbol: str, user_id: str = "system") -> dict:
    async with analysis_lock(symbol, user_id):
        key = (user_id, symbol)
        was_active = is_subscribed(symbol, user_id)
        existing = await get_latest_judgment(symbol, user_id)
        subscribe(symbol, user_id)
        if existing is not None:
            if was_active and key in _last_freshness_check:
                return existing
            last_check = _last_freshness_check.get(key)
            if last_check is not None and time.monotonic() - last_check < settings.watch_refresh_guard_sec:
                return existing

        tick = await build_tick(symbol)
        seed_state(symbol, tick, user_id)
        if existing is not None and cooldown_active(symbol, user_id):
            _last_freshness_check[key] = time.monotonic()
            return existing

        # 실패한 실행은 성공/쿨다운으로 기록하지 않아 다음 요청에서 재시도한다.
        doc = await run_judgment_pipeline(symbol, tick, user_id=user_id)
        mark_run(symbol, user_id)
        _last_freshness_check[key] = time.monotonic()
        return doc
