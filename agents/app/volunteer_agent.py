"""
Component C: The Volunteer Matching Agent as a LangGraph graph.

    gather  ->  draft  ->  check  --(problems, fewer than 2 drafts)-->  draft
                             |
                             +--(ok, or out of drafts)-->  finalize  ->  END

  gather    runs allow-listed read-only tools to rank candidate volunteers
  draft     asks the model (or mock) for a candidate selection & rationale
  check     the agent's own quick self-check (counts, required skills)
  finalize  cleans text and returns proposal with recorded tool calls
"""
from __future__ import annotations

import re
from datetime import datetime, timezone
from typing import Any, TypedDict

from langgraph.graph import END, StateGraph

from .llm import LLMClient
from .schemas import (
    VolunteerAgentResponse,
    VolunteerMatchingInput,
    VolunteerProposal,
)
from .tools import ToolBox

MAX_DRAFTS = 2
_URL = re.compile(r"https?://\S+", re.IGNORECASE)


class VolunteerAgentState(TypedDict, total=False):
    inp: VolunteerMatchingInput
    llm: LLMClient
    toolbox: ToolBox
    facts: dict[str, Any]
    proposal: VolunteerProposal | None
    problems: list[str]
    drafts: int


def sanitize_text(text: str) -> str:
    """Strips HTML tags and hyperlinks to protect coordinators from phishing/injection."""
    return _URL.sub("[link removed]", text.replace("<", "").replace(">", "")).strip()


def local_problems(proposal: VolunteerProposal, inp: VolunteerMatchingInput) -> list[str]:
    """Self-check on candidate selection."""
    problems: list[str] = []
    if len(proposal.volunteer_ids) != inp.volunteers_needed:
        problems.append(f"count-mismatch: expected {inp.volunteers_needed} volunteers, but got {len(proposal.volunteer_ids)}.")

    missing_req = [s for s in inp.required_skills if s not in proposal.skills_covered]
    if missing_req:
        problems.append(f"skills-missing: team fails to cover required skills: {', '.join(missing_req)}.")

    return problems


# ----- Graph Nodes -----
def gather(state: VolunteerAgentState) -> VolunteerAgentState:
    inp, tb = state["inp"], state["toolbox"]
    candidates = [c.model_dump() for c in inp.candidates]

    ranked = tb.call(
        "find_volunteers",
        candidates=candidates,
        required_skills=inp.required_skills,
        target_district=inp.district,
    )

    availability_checks = {}
    for candidate in candidates:
        availability_checks[candidate["id"]] = tb.call(
            "check_volunteer_availability",
            volunteer_id=candidate["id"],
            start_time=inp.start_time,
            end_time=inp.end_time,
            availability_windows=candidate.get("availability_windows", []),
            assignments=candidate.get("assignments", []),
        )

    ranked["availabilityChecks"] = availability_checks
    ranked["candidates"] = [
        candidate for candidate in ranked["candidates"]
        if availability_checks[candidate["id"]]["available"]
    ]

    return {"facts": {"ranked": ranked}, "drafts": 0, "problems": []}


def draft(state: VolunteerAgentState) -> VolunteerAgentState:
    inp = state["inp"]
    context = {
        "input": inp.model_dump(),
        "facts": state["facts"],
        "problems": state.get("problems", []),
    }
    proposal = state["llm"].draft_volunteers(context)
    return {"proposal": proposal, "drafts": state["drafts"] + 1}


def check(state: VolunteerAgentState) -> VolunteerAgentState:
    return {"problems": local_problems(state["proposal"], state["inp"])}


def route_after_check(state: VolunteerAgentState) -> str:
    return "draft" if state["problems"] and state["drafts"] < MAX_DRAFTS else "finalize"


def finalize(state: VolunteerAgentState) -> VolunteerAgentState:
    p = state["proposal"]
    inp, tb = state["inp"], state["toolbox"]

    # Record summary tool call
    candidates = [c.model_dump() for c in inp.candidates]
    tb.call("volunteer_roster_summary", selected_ids=p.volunteer_ids, candidates=candidates)

    clean = p.model_copy(update={
        "summary": sanitize_text(p.summary),
        "rationale": [sanitize_text(r) for r in p.rationale if r.strip()][:10],
    })
    return {"proposal": clean}


def _build_graph():
    g = StateGraph(VolunteerAgentState)
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


def run_volunteer_agent(inp: VolunteerMatchingInput, llm: LLMClient) -> VolunteerAgentResponse:
    toolbox = ToolBox()
    final = _GRAPH.invoke({"inp": inp, "llm": llm, "toolbox": toolbox})
    return VolunteerAgentResponse(proposal=final["proposal"], tool_calls=toolbox.records)
