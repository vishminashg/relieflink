"""Only the ASP.NET Core backend may call this service."""
from __future__ import annotations

import hmac

from fastapi import Header, HTTPException, Request, status


def require_service_key(request: Request, x_service_key: str | None = Header(default=None)) -> None:
    expected: str = request.app.state.settings.service_key
    # Constant-time comparison: the response time reveals nothing about how much of the key was right.
    if not x_service_key or not hmac.compare_digest(x_service_key.encode(), expected.encode()):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid or missing service key.")
