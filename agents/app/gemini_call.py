"""One place that calls the Gemini library, shared by all five agents.

A live test showed that Google sometimes answers "503: this model is experiencing high demand" for a few seconds, or "429: too
many requests" on the free tier. Those are not our mistakes and usually pass in a moment, so we try again a couple of times with
a short wait. Anything else (a bad key, a wrong model name, a refused request) is NOT retried, because trying again cannot help.
The caller still treats whatever finally comes back as untrusted and the API still re-checks it.
"""
from __future__ import annotations

import re
import time
from typing import Any, Callable

RETRY_DELAYS_SECONDS = (1.0, 3.0)  # up to three tries in total, about four seconds of waiting at most

# A "thinking" model spends part of this budget before it writes the answer, so it must be generous.
MAX_OUTPUT_TOKENS = 4096


MAX_WORTH_WAITING_SECONDS = 10  # a 429 that says "retry in 8s" is worth one more try; "retry in 11h" is not


def retry_after_seconds(exc: Exception) -> float | None:
    """The wait Google asks for in a 429 message ("Please retry in 11h41m31.3s" or "retry in 8.5s"), or None if it gives none."""
    match = re.search(r"retry in (?:(\d+)h)?(?:(\d+)m)?([\d.]+)s", str(exc))
    if not match:
        return None
    hours, minutes, seconds = (float(g) if g else 0.0 for g in match.groups())
    return hours * 3600 + minutes * 60 + seconds


def is_transient(exc: Exception) -> bool:
    """True for a busy server (5xx), or a rate limit (429) that will clear within seconds. A daily quota is not transient."""
    code = getattr(exc, "code", None)
    if not isinstance(code, int):
        return False
    if 500 <= code <= 599:
        return True
    if code == 429:
        wait = retry_after_seconds(exc)
        return wait is None or wait <= MAX_WORTH_WAITING_SECONDS
    return False


def call_with_retry(call: Callable[..., Any], *args: Any, sleep: Callable[[float], None] = time.sleep, **kwargs: Any) -> Any:
    for attempt in range(len(RETRY_DELAYS_SECONDS) + 1):
        try:
            return call(*args, **kwargs)
        except Exception as exc:
            if attempt >= len(RETRY_DELAYS_SECONDS) or not is_transient(exc):
                raise
            sleep(RETRY_DELAYS_SECONDS[attempt])


def describe_error(exc: Exception) -> str:
    """"ClientError 429 RESOURCE_EXHAUSTED": the kind of error, the HTTP number and Google's status word. Never the key or the prompt."""
    code = getattr(exc, "code", None)
    status = getattr(exc, "status", None)
    return " ".join(str(x) for x in (type(exc).__name__, code, status) if x)


def describe_empty(response: Any) -> str:
    """Why a reply had no usable text, for the log. Never includes the prompt or the key."""
    try:
        reason = response.candidates[0].finish_reason
        return f" (finish reason: {getattr(reason, 'name', reason)})"
    except Exception:
        return ""
