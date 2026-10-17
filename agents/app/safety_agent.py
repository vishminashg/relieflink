"""Component E: deterministic Safety and Validation Agent.

It screens untrusted notes and selects only an active shelter with enough capacity.
The agent never reserves a bed or writes to a database; ASP.NET Core performs those writes
after coordinator approval.
"""
from __future__ import annotations

import re
from typing import Any

from .schemas import SafetyValidationInput, SafetyValidationResponse, ValidatedProposal, ValidationCheck
from .tools import ToolBox

_INJECTION = re.compile(
    r"(?:ignore|disregard|override)\s+(?:all\s+)?(?:previous|prior|system|safety)(?:\s+\w+){0,2}\s+(?:instructions|rules)",
    re.IGNORECASE,
)


def run_safety_agent(inp: SafetyValidationInput) -> SafetyValidationResponse:
    toolbox = ToolBox()
    capacity = toolbox.call(
        "get_shelter_capacity",
        shelters=[s.model_dump() for s in inp.shelters],
        household_size=inp.household_size,
        district=inp.district,
    )
    checks = [
        ValidationCheck(
            name="prompt_injection_screen",
            ok=not bool(_INJECTION.search(inp.notes or "")),
            detail="Notes passed the safety screen." if not _INJECTION.search(inp.notes or "") else "Untrusted notes contain an instruction-like phrase.",
        ),
        ValidationCheck(
            name="shelter_capacity",
            ok=bool(capacity["candidates"]),
            detail="An active shelter has enough available capacity." if capacity["candidates"] else "No active shelter has enough available capacity.",
        ),
    ]
    ok = all(c.ok for c in checks)
    selected = capacity["candidates"][0]["id"] if ok else None
    return SafetyValidationResponse(
        proposal=ValidatedProposal(
            ok=ok,
            shelter_id=selected,
            checks=checks,
            summary="Safety checks passed." if ok else "Safety validation failed; coordinator review is required.",
        ),
        tool_calls=toolbox.records,
    )
