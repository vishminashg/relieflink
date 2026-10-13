"""
The Planner agent as a LangGraph graph.

    gather  ->  draft  ->  check  --(problems, fewer than 2 drafts)-->  draft
                             |
                             +--(ok, or out of drafts)-->  finalize  ->  END

  gather    runs the allow-listed tools to collect facts
  draft     asks the language model (or the mock) for a plan, given the facts and any earlier problems
  check     the agent's own quick self-check, using the same rules the backend will apply
  finalize  cleans the text and returns the plan together with the tool calls

The self-check only saves round trips. The ASP.NET Core backend always validates again and is the authority.
"""
from __future__ import annotations

import re
from datetime import datetime, timezone
from typing import Any, TypedDict

from langgraph.graph import END, StateGraph

from .llm import RESPONSE_LIMIT_HOURS, LLMClient
from .schemas import Plan, PlannerInput, PlannerResponse
from .tools import ToolBox

MAX_DRAFTS = 2
_LEVELS = ["Low", "Medium", "High", "Critical"]
_URL = re.compile(r"https?://\S+", re.IGNORECASE)


class PlanState(TypedDict, total=False):
    inp: PlannerInput
    llm: LLMClient
    toolbox: ToolBox
    now: datetime
    facts: dict[str, Any]
    plan: Plan | None
    problems: list[str]
    drafts: int


def sanitize_text(text: str) -> str:
    """Plan text is shown to coordinators: no markup and no links."""
    return _URL.sub("[link removed]", text.replace("<", "").replace(">", "")).strip()


def local_problems(plan: Plan, inp: PlannerInput) -> list[str]:
    """The agent's self-check. Mirrors the backend rules that a model is most likely to break."""
    problems: list[str] = []
    wanted = {n.category: n.quantity for n in inp.needs}
    seen: dict[str, int] = {}
    for line in plan.needs:
        seen[line.category] = seen.get(line.category, 0) + 1

    missing = [c for c in wanted if c not in seen]
    if missing:
        problems.append(f"needs-covered: add plan lines for {', '.join(missing)}.")
    extra = [c for c in seen if c not in wanted]
    repeated = [c for c, n in seen.items() if n > 1]
    if extra or repeated:
        problems.append(f"no-invented-needs: remove {', '.join(extra + repeated)}; use only the requested needs, once each.")
    for line in plan.needs:
        if line.category in wanted and not 1 <= line.quantity <= wanted[line.category]:
            problems.append(f"quantities-within-request: {line.category} must be between 1 and {wanted[line.category]}.")

    if plan.recommended_urgency not in _LEVELS or _LEVELS.index(plan.recommended_urgency) < _LEVELS.index(inp.urgency):
        problems.append(f"urgency-not-downgraded: recommended urgency must be {inp.urgency} or higher.")
    level = max(plan.recommended_urgency, inp.urgency, key=lambda u: _LEVELS.index(u) if u in _LEVELS else -1)
    limit = RESPONSE_LIMIT_HOURS.get(level, 6)
    if not 1 <= plan.target_response_hours <= limit:
        problems.append(f"response-time-matches-urgency: target hours must be between 1 and {limit}.")
    return problems


# ----- graph nodes -----
def gather(state: PlanState) -> PlanState:
    inp, tb = state["inp"], state["toolbox"]
    needs = [{"category": n.category, "quantity": n.quantity} for n in inp.needs]
    facts = {
        "triage": tb.call("triage_breakdown", score=inp.triage_score, urgency=inp.urgency, reasons=inp.triage_reasons),
        "priority": tb.call("need_priority_order", needs=needs),
        "vulnerability": tb.call("vulnerability_summary", household=inp.household_size,
                                 elderly=inp.elderly_count, children=inp.children_count, flooded=inp.home_flooded),
        "deadline": tb.call("deadline_pressure", needed_by=inp.needed_by, now=state["now"]),
    }
    return {"facts": facts, "drafts": 0, "problems": []}


def draft(state: PlanState) -> PlanState:
    inp = state["inp"]
    context = {
        "input": inp.model_dump(),                       # snake_case keys for the model layer
        "facts": state["facts"],
        "problems": (inp.feedback.validation_failures if inp.feedback else []) + state.get("problems", []),
        "coordinator_comment": inp.feedback.coordinator_comment if inp.feedback else None,
    }
    plan = state["llm"].draft_plan(context)
    return {"plan": plan, "drafts": state["drafts"] + 1}


def check(state: PlanState) -> PlanState:
    return {"problems": local_problems(state["plan"], state["inp"])}


def route_after_check(state: PlanState) -> str:
    return "draft" if state["problems"] and state["drafts"] < MAX_DRAFTS else "finalize"


def finalize(state: PlanState) -> PlanState:
    p = state["plan"]
    clean = p.model_copy(update={
        "summary": sanitize_text(p.summary),
        "rationale": [sanitize_text(r) for r in p.rationale if r.strip()][:10],
        "risks": [sanitize_text(r) for r in p.risks if r.strip()][:10],
        "needs": [n.model_copy(update={"action": sanitize_text(n.action)}) for n in p.needs],
    })
    return {"plan": clean}


def _build_graph():
    g = StateGraph(PlanState)
    g.add_node("gather", gather)
    g.add_node("draft", draft)
    g.add_node("check", check)
    g.add_node("finalize", finalize)
    g.set_entry_point("gather")
    g.add_edge("gather", "draft")
    g.add_edge("draft", "check")
    g.add_conditional_edges("check", route_after_check, {"draft": "draft", "finalize": "finalize"})
    g.add_edge("finalize", END)
    return g.compile()


_GRAPH = _build_graph()


def run_planner(inp: PlannerInput, llm: LLMClient, now: datetime | None = None) -> PlannerResponse:
    toolbox = ToolBox()
    final = _GRAPH.invoke({"inp": inp, "llm": llm, "toolbox": toolbox, "now": now or datetime.now(timezone.utc)})
    return PlannerResponse(plan=final["plan"], tool_calls=toolbox.records)
