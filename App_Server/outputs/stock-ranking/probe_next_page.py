"""Read-only check for duplicate next-page ranking responses."""
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
    first = original_get(*args, **kwargs)
    first_rows = core.parse_rank_rows(first.json())
    first_codes = [r.get('mksc_shrn_iscd') or r.get('stck_shrn_iscd') for r in first_rows]
    time.sleep(0.4)
    variants = [('next_header', {'tr_cont': 'N'}, {})]
    if 'fluctuation' in args[0]:
        variants.append(('count_100', {}, {'FID_INPUT_CNT_1': '100'}))
    results = []
    for label, extra_headers, extra_params in variants:
        options = dict(kwargs)
        options['headers'] = {**options['headers'], **extra_headers}
        options['params'] = {**options['params'], **extra_params}
        response = original_get(*args, **options)
        body = response.json()
        rows = core.parse_rank_rows(body)
        codes = [r.get('mksc_shrn_iscd') or r.get('stck_shrn_iscd') for r in rows]
        results.append({'variant': label, 'rows': len(rows), 'same_symbols': codes == first_codes,
                        'continuation': response.headers.get('tr_cont'), 'rt_cd': body.get('rt_cd')})
        time.sleep(0.4)
    captured.extend(results)
    return first

requests.get = capture
results = []
for rank in ['volume', 'change-rate', 'market-cap', 'execution-strength']:
    captured.clear()
    try:
        core.get_kis_stock_rank(rank, 30)
        result = {'rank': rank, 'checks': list(captured)}
    except Exception as error:
        result = {'rank': rank, 'error': type(error).__name__, 'checks': list(captured)}
    results.append(result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    time.sleep(0.6)
Path('outputs/stock-ranking/next_page_probe.json').write_text(
    json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
