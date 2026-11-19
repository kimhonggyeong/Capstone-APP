import asyncio
from contextlib import suppress

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from services.ramen.api.routes import judgment, compare, history, analyze, watch
from services.ramen.config import settings
from services.ramen.marketdata.poller import run_polling_loop

app = FastAPI(title="AI 판단 서비스")
app.add_middleware(CORSMiddleware, allow_origins=settings.cors_allow_origins,
                   allow_methods=["*"], allow_headers=["*"])
for router in (judgment.router, compare.router, history.router, analyze.router, watch.router):
    app.include_router(router)

_polling_task: asyncio.Task | None = None


@app.on_event("startup")
async def start_kis_polling():
    global _polling_task
    if not settings.kis_app_key or not settings.kis_app_secret:
        return
    if _polling_task is None or _polling_task.done():
        # 구독이 없으면 KIS/LLM을 호출하지 않는 동적 감시 루프다.
        _polling_task = asyncio.create_task(run_polling_loop(settings.poll_interval_sec))


@app.on_event("shutdown")
async def stop_kis_polling():
    global _polling_task
    if _polling_task is not None:
        _polling_task.cancel()
        with suppress(asyncio.CancelledError):
            await _polling_task
        _polling_task = None


@app.get("/health")
async def health():
    return {"status": "ok"}
