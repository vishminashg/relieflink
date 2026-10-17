"""
Component B: The Supply Agent as a LangGraph graph.

    gather  ->  draft  ->  check  --(problems, fewer than 2 drafts)-->  draft
                             |
                             +--(ok, or out of drafts)-->  finalize  ->  END

  gather    runs allow-listed read-only tools to check depots and stock availability
  draft     asks the model (or mock) for an allocation proposal
  check     the agent's own quick self-check (validates quantities and stock limits)
  finalize  cleans text and returns proposal with recorded tool calls
"""
from __future__ import annotations

import re
from typing import Any, TypedDict

from langgraph.graph import END, StateGraph

from .llm import LLMClient
from .schemas import (
    SupplyAgentResponse,
    SupplyMatchingInput,
    SupplyProposal,
)
from .tools import ToolBox

MAX_DRAFTS = 2
_URL = re.compile(r"https?://\S+", re.IGNORECASE)


class SupplyAgentState(TypedDict, total=False):
    inp: SupplyMatchingInput
    llm: LLMClient
    toolbox: ToolBox
    facts: dict[str, Any]
    proposal: SupplyProposal | None
    problems: list[str]
    drafts: int


def sanitize_text(text: str) -> str:
    """Strips HTML tags and hyperlinks to protect coordinators from phishing/injection."""
    return _URL.sub("[link removed]", text.replace("<", "").replace(">", "")).strip()


def local_problems(proposal: SupplyProposal, inp: SupplyMatchingInput) -> list[str]:
    """Self-check on proposed stock allocations."""
    problems: list[str] = []

    for line in proposal.lines:
        if line.quantity_allocated <= 0:
            problems.append(f"non-positive-quantity: line {line.item_code} has quantity {line.quantity_allocated}.")

    # Group allocated by category
    alloc_by_cat: dict[str, int] = {}
    for line in proposal.lines:
        alloc_by_cat[line.category] = alloc_by_cat.get(line.category, 0) + line.quantity_allocated

    for need in inp.needs:
        alloc = alloc_by_cat.get(need.category, 0)
        if alloc > need.quantity:
            problems.append(f"over-allocated: category {need.category} allocated {alloc} exceeding requested {need.quantity}.")

    return problems


def node_gather(state: SupplyAgentState) -> dict[str, Any]:
    inp = state["inp"]
    tb = state["toolbox"]

    depots_data = [d.model_dump(by_alias=False) for d in inp.available_depots]
    depots_result = tb.call("list_depots", depots=depots_data, target_district=inp.district)

    stock_checks: dict[str, Any] = {}
    for need in inp.needs:
        stock_checks[need.category] = tb.call(
            "check_stock",
            depots=depots_data,
            needed_category=need.category,
            needed_qty=need.quantity,
            target_district=inp.district
        )

    facts = {
        "ranked_depots": depots_result,
        "stock_by_category": stock_checks,
    }
    return {"facts": facts, "drafts": 0}


def node_draft(state: SupplyAgentState) -> dict[str, Any]:
    inp = state["inp"]
    llm = state["llm"]
    facts = state["facts"]
    problems = state.get("problems", [])
    drafts = state.get("drafts", 0) + 1

    context = {
        "input": inp.model_dump(by_alias=False),
        "facts": facts,
        "attempt": inp.attempt,
        "problems": problems,
    }
    proposal = llm.draft_supply(context)
    return {"proposal": proposal, "drafts": drafts}


def node_check(state: SupplyAgentState) -> dict[str, Any]:
    proposal = state["proposal"]
    inp = state["inp"]
    if proposal is None:
        return {"problems": ["no proposal produced"]}
    return {"problems": local_problems(proposal, inp)}


def route_after_check(state: SupplyAgentState) -> str:
    if state.get("problems") and state.get("drafts", 0) < MAX_DRAFTS:
        return "draft"
    return "finalize"


def node_finalize(state: SupplyAgentState) -> dict[str, Any]:
    proposal = state["proposal"]
    tb = state["toolbox"]
    inp = state["inp"]

    if proposal is None:
        raise RuntimeError("Supply agent finalized without a proposal.")

    # Call summary tool to record total units and shortages
    lines_dump = [line.model_dump(by_alias=False) for line in proposal.lines]
    needs_dump = [need.model_dump(by_alias=False) for need in inp.needs]
    tb.call("supply_allocation_summary", lines=lines_dump, needs=needs_dump)

    clean_summary = sanitize_text(proposal.summary)
    clean_rationale = [sanitize_text(r) for r in proposal.rationale]
    clean_risks = [sanitize_text(rk) for r in proposal.risks for rk in [r] if rk]

    final_proposal = proposal.model_copy(update={
        "summary": clean_summary,
        "rationale": clean_rationale,
        "risks": clean_risks
    })
    return {"proposal": final_proposal}


def build_supply_graph() -> StateGraph:
    g = StateGraph(SupplyAgentState)
    g.add_node("gather", node_gather)
    g.add_node("draft", node_draft)
    g.add_node("check", node_check)
    g.add_node("finalize", node_finalize)

    g.set_entry_point("gather")
    g.add_edge("gather", "draft")
    g.add_edge("draft", "check")
    g.add_conditional_edges("check", route_after_check, {"draft": "draft", "finalize": "finalize"})
    g.add_edge("finalize", END)
    return g


def run_supply_agent(inp: SupplyMatchingInput, llm: LLMClient) -> SupplyAgentResponse:
    toolbox = ToolBox()
    graph = build_supply_graph().compile()
    final_state = graph.invoke({
        "inp": inp,
        "llm": llm,
        "toolbox": toolbox,
    })
    return SupplyAgentResponse(
        proposal=final_state["proposal"],
        tool_calls=toolbox.records
    )
