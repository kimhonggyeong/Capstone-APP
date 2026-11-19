"""Minimal Gemini requests for diagnosis; never prints credentials or user data."""

import argparse
import json
from pathlib import Path
import re
import time
from datetime import datetime, timezone

from dotenv import dotenv_values
from google import genai
from google.genai import types


parser = argparse.ArgumentParser()
parser.add_argument("--timeout", type=int, default=15)
parser.add_argument("--models", nargs="+", default=[
    "gemini-3.6-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite",
])
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
key = dotenv_values(root / ".env").get("GEMINI_API_KEY", "")
if not key:
    raise SystemExit("GEMINI_API_KEY is missing from the root .env")


def clean(value):
    text = str(value).replace(key, "[REDACTED]")
    return re.sub(r"AIza[0-9A-Za-z_-]{20,}", "[REDACTED]", text)[:2500]


results = []
for model in args.models:
    started = time.monotonic()
    result = {
        "time_utc": datetime.now(timezone.utc).isoformat(),
        "model": model,
        "timeout_seconds": args.timeout,
        "attempts": 1,
        "request": "Minimal JSON diagnostic; max_output_tokens=256",
    }
    try:
        with genai.Client(
            api_key=key,
            http_options=types.HttpOptions(
                timeout=args.timeout * 1000,
                retry_options=types.HttpRetryOptions(attempts=1),
            ),
        ) as client:
            response = client.models.generate_content(
                model=model,
                contents='Return only this JSON object: {"ok": true}',
                config=types.GenerateContentConfig(
                    temperature=0,
                    response_mime_type="application/json",
                    max_output_tokens=256,
                ),
            )
        result.update(
            ok=True,
            response=clean(getattr(response, "text", "")),
            finish_reasons=[str(c.finish_reason) for c in (response.candidates or [])],
        )
    except Exception as error:
        result.update(
            ok=False,
            error_type=type(error).__name__,
            code=getattr(error, "code", None),
            status=clean(getattr(error, "status", "")),
            message=clean(getattr(error, "message", str(error))),
        )
    result["elapsed_seconds"] = round(time.monotonic() - started, 2)
    results.append(result)
    print(json.dumps(result, ensure_ascii=False), flush=True)

stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
output = Path(__file__).parent / f"probe-{stamp}.json"
output.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
