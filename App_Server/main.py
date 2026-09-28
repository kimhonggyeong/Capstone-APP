"""Single-port entry point for all Anttitude Python services."""

from __future__ import annotations

from dotenv import load_dotenv

# Load the shared environment before importing services. Several source
# services construct their settings objects at import time.
load_dotenv(override=False)

from services.core.main import app  # noqa: E402
from services.legal_chatbot.main import app as legal_chatbot_app  # noqa: E402
from services.market_reaction.api.routes import router as market_reaction_router  # noqa: E402
from services.market_reaction.main import app as market_reaction_app  # noqa: E402
from services.ramen.api.routes import analyze, compare, history, judgment  # noqa: E402
from services.ramen.main import app as ramen_app  # noqa: E402
from services.scenario.main import app as scenario_app  # noqa: E402
from services.scenario.routes import mypage as scenario_mypage  # noqa: E402
from services.scenario.routes import scenario_play, scenario_scoring  # noqa: E402


SUB_APPS = (scenario_app, ramen_app, market_reaction_app, legal_chatbot_app)


@app.on_event("startup")
async def startup_mounted_services() -> None:
    """Run startup handlers that Starlette does not run for mounted apps."""
    for sub_app in SUB_APPS:
        await sub_app.router.startup()


@app.on_event("shutdown")
async def shutdown_mounted_services() -> None:
    for sub_app in reversed(SUB_APPS):
        await sub_app.router.shutdown()


@app.get("/health", tags=["system"])
async def unified_health() -> dict[str, object]:
    return {
        "status": "ok",
        "service": "Full Server",
        "port": 8000,
        "services": {
            "core": "/docs",
            "scenario": "/scenario/docs",
            "ai_ramen": "/ai-ramen/docs",
            "market_reaction": "/market-reaction/docs",
            "legal_chatbot": "/legal-chatbot/docs",
        },
    }


# Preserve the original scenario URLs used by the existing frontend, including
# /api/scenarios and /api/users/{user_id}/sessions. The prefixed mount below is
# retained for independent documentation and namespaced access.
app.include_router(scenario_scoring.router)
app.include_router(scenario_play.router)
app.include_router(scenario_mypage.router)

# Preserve the original AI judgment and market-reaction URLs used by Unity.
app.include_router(judgment.router)
app.include_router(compare.router)
app.include_router(history.router)
app.include_router(analyze.router)
app.include_router(market_reaction_router)


# Mount last: the core application's existing routes remain unchanged, while
# formerly conflicting /health and service endpoints receive stable prefixes.
app.mount("/scenario", scenario_app, name="scenario")
app.mount("/ai-ramen", ramen_app, name="ai-ramen")
app.mount("/market-reaction", market_reaction_app, name="market-reaction")
app.mount("/legal-chatbot", legal_chatbot_app, name="legal-chatbot")




