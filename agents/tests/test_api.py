import pytest
from fastapi.testclient import TestClient

from app.llm import LLMError, MockLLM
from app.main import create_app

from .helpers import KEY, payload, settings

H = {"X-Service-Key": KEY}


def client(**s) -> TestClient:
    return TestClient(create_app(settings(**s)))


# ----- authentication -----
def test_health_is_open_and_reports_the_mode():
    response = client().get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "mode": "mock"}


@pytest.mark.parametrize("headers", [{}, {"X-Service-Key": ""}, {"X-Service-Key": "wrong-key-0123456789"},
                                     {"X-Service-Key": KEY + "x"}, {"X-Service-Key": KEY[:-1]}])
def test_plan_needs_the_exact_service_key(headers):
    response = client().post("/plan", json=payload(), headers=headers)

    assert response.status_code == 401


def test_documentation_pages_are_not_exposed():
    c = client()

    for path in ["/docs", "/redoc", "/openapi.json"]:
        assert c.get(path).status_code == 404


# ----- the happy path and the wire format -----
def test_plan_returns_camel_case_json_the_backend_expects():
    response = client().post("/plan", json=payload(), headers=H)

    assert response.status_code == 200
    body = response.json()
    assert set(body) == {"plan", "toolCalls"}
    assert set(body["plan"]) == {"summary", "recommendedUrgency", "targetResponseHours", "needs", "rationale", "risks"}
    assert body["plan"]["recommendedUrgency"] == "Critical"
    assert set(body["plan"]["needs"][0]) == {"category", "quantity", "action"}
    assert set(body["toolCalls"][0]) == {"name", "arguments", "result", "succeeded", "durationMs"}
    assert len(body["toolCalls"]) == 4


def test_snake_case_names_are_not_part_of_the_contract_output():
    text = client().post("/plan", json=payload(), headers=H).text

    assert "recommended_urgency" not in text and "tool_calls" not in text


# ----- input validation -----
@pytest.mark.parametrize("change", [
    {"urgency": "Banana"},
    {"householdSize": 0},
    {"householdSize": 51},
    {"needs": []},
    {"needs": [{"category": "Gold", "quantity": 1}]},
    {"needs": [{"category": "Food", "quantity": 0}]},
    {"needs": [{"category": "Food", "quantity": 1001}]},
    {"triageScore": 101},
    {"notes": "x" * 2001},
    {"district": "x" * 41},
    {"attempt": 0},
])
def test_invalid_input_is_rejected_with_422(change):
    response = client().post("/plan", json=payload(**change), headers=H)

    assert response.status_code == 422


def test_missing_required_fields_are_rejected():
    body = payload()
    del body["needs"]

    assert client().post("/plan", json=body, headers=H).status_code == 422


def test_unauthenticated_requests_are_rejected_before_input_is_looked_at():
    assert client().post("/plan", json={"nonsense": True}).status_code == 401


# ----- model failure -----
def test_a_model_failure_becomes_502_without_leaking_details():
    class Down:
        def draft_plan(self, context):
            raise LLMError("quota exceeded for key AIza-SECRET")

    c = TestClient(create_app(settings(), llm=Down()))

    response = c.post("/plan", json=payload(), headers=H)

    assert response.status_code == 502
    assert "AIza" not in response.text and "quota" not in response.text


# ----- start-up safety -----
@pytest.mark.parametrize("changes", [
    {"service_key": ""},
    {"service_key": "short"},
    {"llm_mode": "banana"},
    {"llm_mode": "gemini", "gemini_api_key": ""},
    {"mock_fault": "explode"},
])
def test_the_service_refuses_to_start_with_unsafe_settings(changes):
    with pytest.raises(RuntimeError):
        create_app(settings(**changes))


# ----- demo faults (off by default) -----
def test_faults_are_off_by_default():
    body = client().post("/plan", json=payload(), headers=H).json()

    assert len(body["plan"]["needs"]) == 3 and body["plan"]["recommendedUrgency"] == "Critical"


def test_fault_drop_need_only_on_the_first_attempt():
    c = client(mock_fault="drop_need_first_attempt")

    first = c.post("/plan", json=payload(attempt=1), headers=H).json()
    second = c.post("/plan", json=payload(attempt=2), headers=H).json()

    assert len(first["plan"]["needs"]) == 2
    assert len(second["plan"]["needs"]) == 3


def test_fault_lower_urgency():
    body = client(mock_fault="lower_urgency").post("/plan", json=payload(), headers=H).json()

    assert body["plan"]["recommendedUrgency"] == "Low" and body["plan"]["targetResponseHours"] == 72


def test_the_mock_is_the_default_model():
    c = TestClient(create_app(settings()))
    assert isinstance(c.app.state.settings, type(settings())) and c.get("/health").json()["mode"] == "mock"
    assert MockLLM().draft_plan is not None
