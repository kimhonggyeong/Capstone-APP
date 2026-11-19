"""Read-only KIS ranking response metadata; never starts app background jobs."""
import json
import time
from pathlib import Path
import requests
from services.core import main as core

cached = json.loads(Path('kis_token_real.json').read_text(encoding='utf-8-sig'))
core.get_access_token = lambda: cached['access_token']
original_get = requests.get
captured = []

def capture(*args, **kwargs):
    response = original_get(*args, **kwargs)
    payload = response.json()
    rows = core.parse_rank_rows(payload)
    captured.append({
        'raw_rows': len(rows),
        'continuation': response.headers.get('tr_cont'),
        'rt_cd': payload.get('rt_cd'),
        'msg_cd': payload.get('msg_cd'),
        'first_symbol': core.get_rank_stock_symbol(rows[0]) if rows else None,
        'last_symbol': core.get_rank_stock_symbol(rows[-1]) if rows else None,
    })
    return response

requests.get = capture
results = []
for rank in ['volume', 'change-rate', 'market-cap', 'execution-strength']:
    captured.clear()
    try:
        items = core.get_kis_stock_rank(rank, 30)
        result = {'rank': rank, 'items': len(items), 'responses': list(captured)}
    except Exception as error:
        result = {'rank': rank, 'error': type(error).__name__, 'responses': list(captured)}
    results.append(result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    time.sleep(0.6)
Path('outputs/stock-ranking/pagination_probe.json').write_text(
    json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
