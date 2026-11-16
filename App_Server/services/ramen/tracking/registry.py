"""사용자별 감시 구독. 시세는 종목별로 수집하고 판단 이력은 분리한다."""
import asyncio
import time

from services.ramen.config import settings

_subscriptions: dict[tuple[str, str], float] = {}
_analysis_locks: dict[tuple[str, str], asyncio.Lock] = {}


def analysis_lock(symbol: str, user_id: str = "system") -> asyncio.Lock:
    return _analysis_locks.setdefault((user_id, symbol), asyncio.Lock())


def subscribe(symbol: str, user_id: str = "system") -> None:
    _subscriptions[(user_id, symbol)] = time.monotonic()


def unsubscribe(symbol: str, user_id: str = "system") -> None:
    _subscriptions.pop((user_id, symbol), None)


def _prune() -> None:
    now = time.monotonic()
    for key, heartbeat in list(_subscriptions.items()):
        if now - heartbeat >= settings.watch_ttl_sec:
            _subscriptions.pop(key, None)


def is_subscribed(symbol: str, user_id: str = "system") -> bool:
    _prune()
    return (user_id, symbol) in _subscriptions


def active_symbols() -> list[str]:
    _prune()
    return list(dict.fromkeys(symbol for _, symbol in _subscriptions))


def active_users(symbol: str) -> list[str]:
    _prune()
    return [user_id for user_id, watched in _subscriptions if watched == symbol]
