from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient

from app.config import Settings
from app.llm import MockLLM
from app.main import create_app
from app.schemas import (
    DepotOption,
    SupplyItemStock,
    SupplyMatchingInput,
    SupplyNeed,
)
from app.supply_agent import run_supply_agent
from app.tools import check_stock, list_depots, supply_allocation_summary

SERVICE_KEY = "test-service-key-0123456789"


def make_sample_depots() -> list[DepotOption]:
    return [
        DepotOption(
            id="dep-col-1",
            code="DEP-COL-01",
            name="Colombo Hub",
            district="Colombo",
            items=[
                SupplyItemStock(id="it-wtr", code="SKU-WTR-5L", name="Water 5L", category="Water", quantity_available=1000),
                SupplyItemStock(id="it-food", code="SKU-RAT-DRY", name="Ration Pack", category="Food", quantity_available=500),
            ]
        ),
        DepotOption(
            id="dep-klu-1",
            code="DEP-KLU-01",
            name="Kalutara Depot",
            district="Kalutara",
            items=[
                SupplyItemStock(id="it-wtr-k", code="SKU-WTR-5L", name="Water 5L", category="Water", quantity_available=200),
                SupplyItemStock(id="it-med-k", code="SKU-MED-FAK", name="Medical Kit", category="Medical", quantity_available=50),
            ]
        ),
    ]


def test_list_depots_tool_ranks_by_district():
    depots_data = [d.model_dump(by_alias=False) for d in make_sample_depots()]
    result = list_depots(depots_data, target_district="Kalutara")
    depots = result["depots"]

    assert len(depots) == 2
    # Kalutara depot is in target district, so ranked first
    assert depots[0]["id"] == "dep-klu-1"
    assert depots[0]["sameDistrict"] is True
    assert depots[1]["id"] == "dep-col-1"
    assert depots[1]["sameDistrict"] is False


def test_check_stock_tool_identifies_shortages_and_candidates():
    depots_data = [d.model_dump(by_alias=False) for d in make_sample_depots()]

    # Case 1: Sufficient stock
    res1 = check_stock(depots_data, needed_category="Water", needed_qty=150, target_district="Kalutara")
    assert res1["isFullyCovered"] is True
    assert res1["shortage"] == 0
    assert len(res1["candidateOptions"]) == 2
    # Kalutara option listed first due to sameDistrict
    assert res1["candidateOptions"][0]["depotId"] == "dep-klu-1"

    # Case 2: Shortage
    res2 = check_stock(depots_data, needed_category="Medical", needed_qty=80, target_district="Kalutara")
    assert res2["isFullyCovered"] is False
    assert res2["shortage"] == 30  # 80 needed, 50 available


def test_supply_allocation_summary_tool():
    lines = [
        {"category": "Water", "quantity_allocated": 100, "depot_id": "dep-klu-1"},
        {"category": "Food", "quantity_allocated": 20, "depot_id": "dep-col-1"},
    ]
    needs = [
        {"category": "Water", "quantity": 100},
        {"category": "Food", "quantity": 30},
    ]

    summary = supply_allocation_summary(lines, needs)
    assert summary["totalUnitsAllocated"] == 120
    assert summary["uniqueDepotsCount"] == 2
    assert summary["isAllCovered"] is False
    assert len(summary["shortages"]) == 1
    assert summary["shortages"][0]["category"] == "Food"
    assert summary["shortages"][0]["shortage"] == 10


def test_run_supply_agent_full_workflow():
    inp = SupplyMatchingInput(
        request_id="req-12345",
        district="Kalutara",
        needs=[
            SupplyNeed(category="Water", quantity=50),
            SupplyNeed(category="Medical", quantity=10),
        ],
        available_depots=make_sample_depots(),
        notes="Urgent water needed"
    )

    response = run_supply_agent(inp, MockLLM())
    proposal = response.proposal

    assert len(proposal.lines) == 2
    assert proposal.shortages == []
    assert "Kalutara" in proposal.summary
    assert len(proposal.rationale) >= 2

    # Check recorded tool calls
    tool_names = [call.name for call in response.tool_calls]
    assert "list_depots" in tool_names
    assert "check_stock" in tool_names
    assert "supply_allocation_summary" in tool_names


def test_supply_agent_api_endpoint():
    app = create_app(Settings(service_key=SERVICE_KEY, llm_mode="mock"))
    client = TestClient(app)

    payload = {
        "requestId": "req-9999",
        "district": "Kalutara",
        "needs": [
            {"category": "Water", "quantity": 100}
        ],
        "availableDepots": [
            {
                "id": "dep-1",
                "code": "DEP-KLU-01",
                "name": "Kalutara Depot",
                "district": "Kalutara",
                "items": [
                    {"id": "it-1", "code": "SKU-WTR", "name": "Water", "category": "Water", "quantityAvailable": 500}
                ]
            }
        ],
        "notes": "Emergency relief"
    }

    # Unauthorized without header
    r_unauth = client.post("/agents/supply", json=payload)
    assert r_unauth.status_code == 401

    # Authorized
    r_auth = client.post("/agents/supply", json=payload, headers={"X-Service-Key": SERVICE_KEY})
    assert r_auth.status_code == 200

    data = r_auth.json()
    assert "proposal" in data
    assert "toolCalls" in data
    assert len(data["proposal"]["lines"]) == 1
    assert data["proposal"]["lines"][0]["quantityAllocated"] == 100
