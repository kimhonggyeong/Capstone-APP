"""체결강도 전용 KIS API와 Unity 순위 응답 계약을 검증한다."""
import asyncio
from unittest.mock import Mock

import httpx
import pytest
from fastapi import HTTPException

from main import app
from services.core import main as core


def stock_row(symbol="005930", strength="125.50", **extra):
    return {
        "stck_shrn_iscd": symbol,
        "hts_kor_isnm": "테스트 종목",
        "stck_prpr": "193200",
        "prdy_ctrt": "-1.83",
        "acml_vol": "1234567",
        "tday_rltv": strength,
        **extra,
    }


@pytest.fixture
def kis(monkeypatch):
    monkeypatch.setattr(core, "KIS_MODE", "real")
    monkeypatch.setattr(core, "get_access_token", Mock(return_value="test-token"))
    monkeypatch.setattr(core, "latest_prices", {})
    monkeypatch.setattr(core, "latest_names", {})
    monkeypatch.setattr(core, "stock_master", {})
    # The old volume-rank fallback must never be used for execution strength.
    monkeypatch.setattr(core, "request_kis_volume_rank", Mock(side_effect=AssertionError("Wrong ranking API")))
    request = Mock(return_value={"rt_cd": "0", "output": [stock_row()]})
    monkeypatch.setattr(core, "kis_get_with_retry", request)
    return request


def test_calls_dedicated_api_and_maps_actual_strength(kis):
    items = core.get_kis_execution_strength_rank()
    request = kis.call_args.kwargs
    assert request["url"].endswith("/uapi/domestic-stock/v1/ranking/volume-power")
    assert request["headers"]["tr_id"] == "FHPST01680000"
    assert request["params"]["FID_COND_SCR_DIV_CODE"] == "20168"
    assert request["params"]["FID_TRGT_CLS_CODE"] == "0"
    assert "FID_BLNG_CLS_CODE" not in request["params"]
    assert items[0] == {
        "symbol": "005930", "name": "테스트 종목", "price": 193200,
        "change_rate": -1.83, "volume": 1234567, "trade_value": 0,
        "market_cap": 0, "execution_strength": 125.5, "rank": 1,
    }


def test_filters_supported_symbols_before_limiting_and_numbers_ranks(kis):
    kis.return_value = {"rt_cd": "0", "output": [
        stock_row("Q520073", "999.99"),  # Existing app supports numeric six-digit symbols.
        stock_row("005930", "90.25"),
        stock_row("000660", "220.75"),
        stock_row("035720", "140.00"),
    ]}
    items = core.get_kis_execution_strength_rank(limit=2)
    assert [item["symbol"] for item in items] == ["000660", "035720"]
    assert [item["rank"] for item in items] == [1, 2]


def test_keeps_real_zero_and_skips_missing_values(kis):
    missing = stock_row("035720")
    del missing["tday_rltv"]
    kis.return_value = {"rt_cd": "0", "output": [
        missing, stock_row("005930", 0, cttr="999"), stock_row("000660", ""),
    ]}
    items = core.get_kis_execution_strength_rank()
    assert len(items) == 1
    assert items[0]["symbol"] == "005930"
    assert items[0]["execution_strength"] == 0


def test_empty_market_response_is_an_empty_list(kis):
    kis.return_value = {"rt_cd": "0", "output": []}
    assert core.get_kis_execution_strength_rank() == []


def test_missing_strength_schema_is_not_reported_as_zero(kis):
    row = stock_row()
    del row["tday_rltv"]
    kis.return_value = {"rt_cd": "0", "output": [row]}
    with pytest.raises(HTTPException) as error:
        core.get_kis_execution_strength_rank()
    assert error.value.status_code == 502
    assert "tday_rltv" in error.value.detail


def test_kis_error_is_propagated_without_volume_fallback(kis):
    kis.return_value = {"rt_cd": "1", "msg_cd": "TEST_ERROR", "msg1": "조회 실패"}
    with pytest.raises(HTTPException) as error:
        core.get_kis_execution_strength_rank()
    assert error.value.status_code == 502
    assert "체결강도" in error.value.detail["message"]
    assert kis.call_count == 1


def test_paper_environment_is_rejected_before_external_requests(kis, monkeypatch):
    monkeypatch.setattr(core, "KIS_MODE", "paper")
    with pytest.raises(HTTPException) as error:
        core.get_kis_execution_strength_rank()
    assert error.value.status_code == 503
    kis.assert_not_called()
    core.get_access_token.assert_not_called()


def test_existing_unity_endpoint_and_limit_validation(kis):
    async def check():
        transport = httpx.ASGITransport(app=app)
        async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.get("/stocks/rank?type=execution-strength&limit=1")
            assert response.status_code == 200
            data = response.json()
            assert data["ranking"] == "execution-strength"
            assert data["source"] == "KIS"
            assert data["realtime"] is True
            assert data["as_of"]
            assert data["items"][0]["execution_strength"] == 125.5
            assert (await client.get("/stocks/rank?type=execution-strength&limit=31")).status_code == 422
    asyncio.run(check())


@pytest.mark.parametrize("rank_type,function_name,kwargs", [
    ("volume", "request_kis_volume_rank", {"sort_code": "0", "limit": 3}),
    ("change-rate", "get_kis_change_rate_rank", {}),
    ("market-cap", "get_kis_market_cap_rank", {}),
])
def test_other_rank_types_keep_their_original_dispatch(monkeypatch, rank_type, function_name, kwargs):
    expected = [{"symbol": "005930"}]
    handler = Mock(return_value=expected)
    monkeypatch.setattr(core, function_name, handler)
    assert core.get_kis_stock_rank(rank_type, 3) == expected
    if kwargs:
        handler.assert_called_once_with(**kwargs)
    else:
        handler.assert_called_once_with(3)
