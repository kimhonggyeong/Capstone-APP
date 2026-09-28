import asyncio

import httpx

from main import app


async def _check_routes() -> None:
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(
        transport=transport,
        base_url="http://test",
    ) as client:
        for path in (
            "/health",
            "/scenario/",
            "/ai-ramen/health",
            "/market-reaction/health",
            "/legal-chatbot/health",
            "/scenario/docs",
            "/ai-ramen/docs",
            "/market-reaction/docs",
            "/legal-chatbot/docs",
        ):
            response = await client.get(path)
            assert response.status_code == 200, (path, response.status_code)


def test_mounted_services_respond() -> None:
    asyncio.run(_check_routes())
