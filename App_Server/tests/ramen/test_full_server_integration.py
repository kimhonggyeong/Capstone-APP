"""통합 서버의 로그인/이력/Unity 호환 검증. 외부 API와 DB는 호출하지 않는다."""
import asyncio
import copy
import importlib

import httpx
import pytest
from fastapi import FastAPI

from services.ramen import pipeline
from services.ramen.api.routes import analyze, compare, history, judgment, watch
from services.ramen.auth import get_current_user_id
from services.ramen.config import settings
from services.ramen.history import repository
from services.ramen.marketdata import cold_start, poller
from services.ramen.tracking import registry
from services.ramen.triggers import event_listener


def document(user_id="alice", probabilities=True):
    doc = dict(symbol="005930", user_id=user_id, judge="관망", confidence=93.4,
               time="2026-11-09T00:00:00+00:00", reason="분석 설명", changed=False,
               factors=[dict(factor="거래량", direction="긍정", weight=30)])
    if probabilities:
        doc["probabilities"] = {"매수": 3.3, "매도": 3.3, "관망": 93.4}
    return doc


@pytest.fixture(autouse=True)
def clear_runtime_state(monkeypatch):
    registry._subscriptions.clear()
    registry._analysis_locks.clear()
    cold_start._last_freshness_check.clear()
    event_listener._last_state.clear()
    event_listener._last_run_at.clear()
    poller._daily_cache.clear()
    monkeypatch.setattr(settings, "kis_app_key", "test-key")
    monkeypatch.setattr(settings, "kis_app_secret", "test-secret")


def api_app(authenticated=True):
    app = FastAPI()
    for router in (analyze.router, judgment.router, history.router, compare.legacy_router, watch.router):
        app.include_router(router)
    native = FastAPI()
    native.include_router(compare.router)
    if authenticated:
        app.dependency_overrides[get_current_user_id] = lambda: "alice"
        native.dependency_overrides[get_current_user_id] = lambda: "alice"
    app.mount("/ai-ramen", native)
    return app


def test_unity_and_native_responses_keep_auth_and_old_history(monkeypatch):
    calls = []

    async def latest(symbol, user_id):
        calls.append((symbol, user_id))
        return document(user_id)

    async def old_history(symbol, user_id, limit):
        calls.append((symbol, user_id))
        return [document(user_id, probabilities=False)]

    async def explanation(*args):
        return "비교 설명"

    for module in (judgment, compare):
        monkeypatch.setattr(module, "get_latest_judgment", latest)
    monkeypatch.setattr(history, "get_history", old_history)
    monkeypatch.setattr(compare, "generate_comparison", explanation)

    async def check():
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=api_app()), base_url="http://test") as client:
            latest_response = await client.get("/judgment/005930")
            assert latest_response.status_code == 200
            assert latest_response.json()["factors"][0]["type"] == "직접"
            history_response = await client.get("/judgment/005930/history")
            assert history_response.status_code == 200
            assert history_response.json()[0]["confidence"] == 93.4
            assert sum(history_response.json()[0]["probabilities"].values()) == 0
            payload = dict(symbol="005930", user_judge="매수")
            legacy = await client.post("/judgment/compare", json=payload)
            native = await client.post("/ai-ramen/judgment/compare", json=payload)
            assert legacy.status_code == native.status_code == 200
            assert legacy.json()["highlighted_factors"] == {"직접요인": ["거래량"], "간접요인": []}
            assert native.json()["highlighted_factors"] == ["거래량"]
            assert native.json()["ai_probabilities"] == legacy.json()["ai_probabilities"]
            assert (await client.get("/judgment/005930/history?limit=0")).status_code == 422
        assert all(user == "alice" for _, user in calls)
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=api_app(False)), base_url="http://test") as client:
            for method, path in (("GET", "/judgment/005930"), ("GET", "/judgment/005930/history"),
                                 ("POST", "/judgment/analyze"), ("POST", "/judgment/compare"),
                                 ("POST", "/judgment/005930/watch"), ("DELETE", "/judgment/005930/watch")):
                response = await client.request(method, path, json=dict(symbol="005930", user_judge="매수"))
                assert response.status_code == 401, path
    asyncio.run(check())


def test_pipeline_scopes_reads_and_saves_probabilities(monkeypatch):
    saved = []
    looked_up = []

    async def latest(symbol, user_id):
        looked_up.append((symbol, user_id))
        return None

    async def save(doc):
        saved.append(copy.deepcopy(doc))

    async def reasoning(*args):
        return "기술적 지표 설명"

    monkeypatch.setattr(pipeline, "get_latest_judgment", latest)
    monkeypatch.setattr(pipeline, "save_judgment", save)
    monkeypatch.setattr(pipeline, "generate_reasoning_summary", reasoning)

    async def check():
        alice = await pipeline.run_judgment_pipeline("005930", {}, user_id="alice")
        bob = await pipeline.run_judgment_pipeline("005930", {}, user_id="bob")
        assert alice["confidence"] == alice["probabilities"][alice["judge"]]
        assert round(sum(alice["probabilities"].values()), 1) == 100
        assert alice["time"].endswith("+00:00")
        assert [d["user_id"] for d in saved] == ["alice", "bob"]
        assert looked_up == [("005930", "alice"), ("005930", "bob")]
    asyncio.run(check())


def test_repository_never_reads_another_users_history(monkeypatch):
    queries = []

    class Cursor:
        def sort(self, *args): return self
        def limit(self, *args): return self
        def __aiter__(self): return self
        async def __anext__(self): raise StopAsyncIteration

    class Collection:
        async def find_one(self, query, **kwargs): queries.append(query)
        def find(self, query):
            queries.append(query)
            return Cursor()

    monkeypatch.setattr(repository, "get_database", lambda: {repository.COLLECTION: Collection()})

    async def check():
        await repository.get_latest_judgment("005930", "alice")
        await repository.get_history("005930", "bob")
        assert queries == [dict(symbol="005930", user_id="alice"), dict(symbol="005930", user_id="bob")]
    asyncio.run(check())


def test_watch_is_user_scoped_and_expires(monkeypatch):
    now = [100.0]
    monkeypatch.setattr(registry.time, "monotonic", lambda: now[0])
    registry.subscribe("005930", "alice")
    registry.subscribe("005930", "bob")
    assert registry.active_symbols() == ["005930"]
    registry.unsubscribe("005930", "alice")
    assert registry.active_users("005930") == ["bob"]
    now[0] += settings.watch_ttl_sec
    assert registry.active_symbols() == []


def test_concurrent_watch_calls_only_analyze_once_per_user(monkeypatch):
    docs = {}
    runs = []

    async def latest(symbol, user_id): return docs.get(user_id)
    async def tick(symbol):
        await asyncio.sleep(0)
        return {"price": 100}
    async def run(symbol, data, user_id):
        runs.append(user_id)
        await asyncio.sleep(0)
        docs[user_id] = document(user_id)
        return docs[user_id]

    monkeypatch.setattr(cold_start, "get_latest_judgment", latest)
    monkeypatch.setattr(cold_start, "build_tick", tick)
    monkeypatch.setattr(cold_start, "run_judgment_pipeline", run)

    async def check():
        await asyncio.gather(*(cold_start.cold_start("005930", "alice") for _ in range(3)))
        await cold_start.cold_start("005930", "bob")
        assert runs == ["alice", "bob"]
        assert event_listener.cooldown_active("005930", "alice")
        assert not event_listener.cooldown_active("005930", "charlie")
    asyncio.run(check())


def test_failed_watch_analysis_retries_existing_history(monkeypatch):
    runs = []
    async def latest(*args): return document()
    async def tick(*args): return {"price": 100}
    async def run(*args, **kwargs):
        runs.append(1)
        if len(runs) == 1: raise RuntimeError("LLM unavailable")
        return document()
    monkeypatch.setattr(cold_start, "get_latest_judgment", latest)
    monkeypatch.setattr(cold_start, "build_tick", tick)
    monkeypatch.setattr(cold_start, "run_judgment_pipeline", run)

    async def check():
        with pytest.raises(RuntimeError): await cold_start.cold_start("005930", "alice")
        assert not event_listener.cooldown_active("005930", "alice")
        await cold_start.cold_start("005930", "alice")
        assert len(runs) == 2
    asyncio.run(check())


def test_manual_analysis_and_watch_share_the_same_lock(monkeypatch):
    docs = {}
    runs = []
    async def latest(symbol, user_id): return docs.get(user_id)
    async def tick(*args): return {"price": 100}
    async def run(symbol, data, user_id):
        runs.append(user_id)
        await asyncio.sleep(0)
        docs[user_id] = document(user_id)
        return docs[user_id]
    monkeypatch.setattr(cold_start, "get_latest_judgment", latest)
    monkeypatch.setattr(cold_start, "build_tick", tick)
    monkeypatch.setattr(cold_start, "run_judgment_pipeline", run)
    monkeypatch.setattr(analyze, "build_tick", tick)
    monkeypatch.setattr(analyze, "run_judgment_pipeline", run)

    async def check():
        await asyncio.gather(
            analyze.analyze_judgment(analyze.AnalyzeRequest(symbol="005930"), "alice"),
            cold_start.cold_start("005930", "alice"),
        )
        assert runs == ["alice"]
    asyncio.run(check())


def test_background_fans_out_one_tick_and_shutdown_cancels(monkeypatch):
    ticks, users = [], []
    async def tick(symbol):
        ticks.append(symbol)
        return {"price": 100}
    async def process(symbol, tick, user_id):
        users.append(user_id)
        if user_id == "alice": raise RuntimeError("one user failed")
    monkeypatch.setattr(poller, "build_tick", tick)
    monkeypatch.setattr(poller, "on_market_tick", process)

    async def check():
        registry.subscribe("005930", "alice")
        registry.subscribe("005930", "bob")
        await poller.poll_symbol("005930")
        assert ticks == ["005930"] and users == ["alice", "bob"]
        service = importlib.import_module("services.ramen.main")
        async def loop(interval): await asyncio.Event().wait()
        monkeypatch.setattr(service, "run_polling_loop", loop)
        await service.start_kis_polling()
        task = service._polling_task
        await service.start_kis_polling()
        assert service._polling_task is task
        await service.stop_kis_polling()
        assert task.cancelled() and service._polling_task is None
    asyncio.run(check())


def test_missing_keys_and_upstream_failure_are_reported(monkeypatch):
    async def failed_tick(symbol): raise RuntimeError("test upstream error")
    monkeypatch.setattr(analyze, "build_tick", failed_tick)

    async def check():
        async with httpx.AsyncClient(transport=httpx.ASGITransport(app=api_app()), base_url="http://test") as client:
            response = await client.post("/judgment/analyze", json={"symbol": "005930"})
            assert response.status_code == 502
            assert "test upstream error" in response.json()["detail"]
            monkeypatch.setattr(settings, "kis_app_key", "")
            for path in ("/judgment/analyze", "/judgment/005930/watch"):
                assert (await client.post(path, json={"symbol": "005930"})).status_code == 503
    asyncio.run(check())
