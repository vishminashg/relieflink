from datetime import datetime, timezone

import pytest
from fastapi.testclient import TestClient

from app.config import Settings
from app.llm import MockLLM
from app.main import create_app
from app.schemas import (
    VolunteerCandidate,
    VolunteerMatchingInput,
)
from app.tools import ToolBox, find_volunteers, volunteer_roster_summary
from app.volunteer_agent import run_volunteer_agent

NOW = datetime(2026, 10, 15, 9, 0, tzinfo=timezone.utc)
SERVICE_KEY = "test-service-key-0123456789"


def make_candidate(id: str, district: str = "Kalutara", skills: list[str] = None, completed: int = 2) -> VolunteerCandidate:
    return VolunteerCandidate(
        id=id,
        name=f"Volunteer {id}",
        district=district,
        skills=skills or ["BoatOperation", "FirstAid"],
        is_verified=True,
        completed_assignments=completed,
    )


def test_find_volunteers_tool_ranks_matching_candidates():
    candidates = [
        {"id": "1", "name": "A", "district": "Jaffna", "skills": ["Cooking"], "completed_assignments": 0},
        {"id": "2", "name": "B", "district": "Kalutara", "skills": ["BoatOperation"], "completed_assignments": 5},
        {"id": "3", "name": "C", "district": "Kalutara", "skills": ["BoatOperation", "FirstAid"], "completed_assignments": 10},
    ]

    result = find_volunteers(candidates, required_skills=["BoatOperation"], target_district="Kalutara")
    ranked = result["candidates"]

    assert len(ranked) == 3
    # Candidate 3 has all required skills, same district, and highest completed assignments
    assert ranked[0]["id"] == "3"
    assert ranked[0]["satisfiesAllRequired"] is True
    assert ranked[0]["sameDistrict"] is True


def test_volunteer_roster_summary_computes_coverage():
    candidates = [
        {"id": "1", "district": "Kalutara", "skills": ["BoatOperation"]},
        {"id": "2", "district": "Kalutara", "skills": ["FirstAid", "Medical"]},
    ]

    summary = volunteer_roster_summary(["1", "2"], candidates)
    assert summary["selectedCount"] == 2
    assert summary["skillsCovered"] == ["BoatOperation", "FirstAid", "Medical"]
    assert summary["districts"] == ["Kalutara"]


def test_volunteer_agent_runs_graph_and_returns_proposal_with_tool_calls():
    c1 = make_candidate("vol-1", "Kalutara", ["BoatOperation", "FirstAid"])
    c2 = make_candidate("vol-2", "Kalutara", ["BoatOperation"])

    inp = VolunteerMatchingInput(
        request_id="req-123",
        district="Kalutara",
        required_skills=["BoatOperation"],
        start_time=NOW,
        end_time=NOW,
        volunteers_needed=1,
        candidates=[c1, c2],
    )

    llm = MockLLM()
    response = run_volunteer_agent(inp, llm)

    assert len(response.proposal.volunteer_ids) == 1
    assert response.proposal.volunteer_ids[0] in ["vol-1", "vol-2"]
    assert "BoatOperation" in response.proposal.skills_covered
    assert len(response.tool_calls) == 4  # find_volunteers, availability checks, and roster summary
    assert any(tc.name == "find_volunteers" for tc in response.tool_calls)
    assert sum(tc.name == "check_volunteer_availability" for tc in response.tool_calls) == 2
    assert any(tc.name == "volunteer_roster_summary" for tc in response.tool_calls)


def test_api_match_volunteers_endpoint_requires_service_key():
    settings = Settings(service_key=SERVICE_KEY, llm_mode="mock")
    app = create_app(settings)

    client = TestClient(app)

    payload = {
        "requestId": "req-1",
        "district": "Kalutara",
        "requiredSkills": ["BoatOperation"],
        "startTime": NOW.isoformat(),
        "endTime": NOW.isoformat(),
        "volunteersNeeded": 1,
        "candidates": [{"id": "vol-1", "district": "Kalutara", "skills": ["BoatOperation"]}],
    }

    # Missing key -> 401
    res = client.post("/agents/volunteers", json=payload)
    assert res.status_code == 401

    # Valid key -> 200
    res = client.post("/agents/volunteers", json=payload, headers={"X-Service-Key": SERVICE_KEY})
    assert res.status_code == 200
    body = res.json()
    assert "proposal" in body
    assert "toolCalls" in body
    assert body["proposal"]["volunteerIds"] == ["vol-1"]
