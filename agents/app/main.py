"""The agent service's HTTP API. Internal only: every call needs the shared service key."""
from __future__ import annotations

import logging
import time

from fastapi import Depends, FastAPI, HTTPException

from .config import Settings
from .llm import LLMClient, LLMError, build_llm
from .planner import run_planner
from .schemas import Plan, PlanNeedLine, PlannerInput, PlannerResponse
from .security import require_service_key

log = logging.getLogger("agents")


def apply_mock_fault(plan: Plan, fault: str, attempt: int) -> Plan:
    """
    TESTING AND DEMO ONLY (MOCK_FAULT). Makes the agent misbehave on purpose so the backend's
    validation and retry logic can be shown working. Off by default.
    """
    if fault == "drop_need_first_attempt" and attempt == 1 and len(plan.needs) > 1:
        return plan.model_copy(update={"needs": plan.needs[1:]})
    if fault == "lower_urgency":
        return plan.model_copy(update={"recommended_urgency": "Low", "target_response_hours": 72})
    return plan


def create_app(settings: Settings | None = None, llm: LLMClient | None = None) -> FastAPI:
    settings = settings or Settings.from_env()
    settings.validate()
    llm = llm or build_llm(settings)

    app = FastAPI(title="ReliefLink agent service", docs_url=None, redoc_url=None, openapi_url=None)
    app.state.settings = settings

    @app.get("/health")
    def health() -> dict[str, str]:
        return {"status": "ok", "mode": settings.llm_mode}

    @app.post("/plan", response_model=PlannerResponse, response_model_by_alias=True,
              dependencies=[Depends(require_service_key)])
    def plan(inp: PlannerInput) -> PlannerResponse:
        started = time.perf_counter()
        try:
            result = run_planner(inp, llm)
        except LLMError as exc:
            log.warning("planner failed for request %s: %s", inp.request_id, exc)
            raise HTTPException(status_code=502, detail="The language model could not produce a plan.") from exc

        if settings.mock_fault != "none":
            result = result.model_copy(update={"plan": apply_mock_fault(result.plan, settings.mock_fault, inp.attempt)})

        # Citizen notes are never logged.
        log.info("planned request %s (attempt %s) in %d ms", inp.request_id, inp.attempt,
                 int((time.perf_counter() - started) * 1000))
        return result

    return app


def app_factory() -> FastAPI:  # used by: uvicorn app.main:app_factory --factory
    return create_app()
