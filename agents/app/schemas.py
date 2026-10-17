"""The contract with the ASP.NET Core backend. Field names are camelCase on the wire."""
from __future__ import annotations

from datetime import datetime
from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

CATEGORIES = ["Food", "Water", "Shelter", "Medical", "Clothing", "Sanitation"]
Category = Literal["Food", "Water", "Shelter", "Medical", "Clothing", "Sanitation"]
Urgency = Literal["Low", "Medium", "High", "Critical"]


class Wire(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class Need(Wire):
    category: Category
    quantity: int = Field(ge=1, le=1000)
    description: str | None = Field(default=None, max_length=300)


class Feedback(Wire):
    validation_failures: list[str] = Field(default_factory=list, max_length=20)
    coordinator_comment: str | None = Field(default=None, max_length=1000)


class PlannerInput(Wire):
    request_id: str
    district: str = Field(max_length=40)
    household_size: int = Field(ge=1, le=50)
    elderly_count: int = Field(ge=0, le=50)
    children_count: int = Field(ge=0, le=50)
    home_flooded: bool
    needed_by: datetime | None = None
    triage_score: int = Field(ge=0, le=100)
    urgency: Urgency
    triage_reasons: list[str] = Field(default_factory=list, max_length=30)
    needs: list[Need] = Field(min_length=1, max_length=10)
    notes: str | None = Field(default=None, max_length=2000)   # written by a citizen: untrusted
    attempt: int = Field(default=1, ge=1, le=20)
    feedback: Feedback | None = None


class PlanNeedLine(Wire):
    category: str
    quantity: int
    action: str


class Plan(Wire):
    summary: str
    recommended_urgency: str
    target_response_hours: int
    needs: list[PlanNeedLine]
    rationale: list[str]
    risks: list[str] = Field(default_factory=list)


class ToolCallRecord(Wire):
    name: str
    arguments: dict[str, Any] | None = None
    result: dict[str, Any] | None = None
    succeeded: bool = True
    duration_ms: int | None = None


class PlannerResponse(Wire):
    plan: Plan
    tool_calls: list[ToolCallRecord]


# Component C: Volunteer Matching Agent
class VolunteerCandidate(Wire):
    id: str
    name: str = ""
    district: str
    skills: list[str] = Field(default_factory=list)
    is_verified: bool = False
    completed_assignments: int = 0


class VolunteerMatchingInput(Wire):
    request_id: str
    district: str
    required_skills: list[str] = Field(default_factory=list)
    start_time: datetime
    end_time: datetime
    volunteers_needed: int = Field(ge=1, le=20)
    candidates: list[VolunteerCandidate] = Field(default_factory=list)
    notes: str | None = None
    attempt: int = Field(default=1, ge=1, le=10)


class VolunteerProposal(Wire):
    volunteer_ids: list[str]
    skills_covered: list[str]
    summary: str
    rationale: list[str] = Field(default_factory=list)


class VolunteerAgentResponse(Wire):
    proposal: VolunteerProposal
    tool_calls: list[ToolCallRecord]

