"""Retry on a busy Gemini server, never on a mistake of ours."""
import pytest

from app.gemini_call import MAX_OUTPUT_TOKENS, RETRY_DELAYS_SECONDS, call_with_retry, describe_empty, is_transient


class Boom(Exception):
    def __init__(self, code=None):
        super().__init__(f"boom {code}")
        self.code = code


def flaky(failures, error):
    state = {"calls": 0}

    def call(**kwargs):
        state["calls"] += 1
        if state["calls"] <= failures:
            raise error
        return kwargs.get("value", "ok")

    return call, state


@pytest.mark.parametrize("code", [429, 500, 502, 503, 504, 599])
def test_busy_and_rate_limit_codes_are_transient(code):
    assert is_transient(Boom(code))


@pytest.mark.parametrize("code", [400, 401, 403, 404, 499, 600, None])
def test_everything_else_is_not_transient(code):
    assert not is_transient(Boom(code))


def test_a_plain_exception_is_not_transient():
    assert not is_transient(ValueError("schema"))


def test_a_busy_server_is_retried_until_it_answers():
    call, state = flaky(2, Boom(503))
    waits = []

    assert call_with_retry(call, sleep=waits.append, value="plan") == "plan"
    assert state["calls"] == 3
    assert waits == list(RETRY_DELAYS_SECONDS)


def test_it_gives_up_after_the_last_try_and_raises_the_original_error():
    error = Boom(503)
    call, state = flaky(99, error)

    with pytest.raises(Boom) as raised:
        call_with_retry(call, sleep=lambda _: None)

    assert raised.value is error
    assert state["calls"] == len(RETRY_DELAYS_SECONDS) + 1


@pytest.mark.parametrize("error", [Boom(400), Boom(401), Boom(403), Boom(404), ValueError("bad schema")])
def test_a_mistake_of_ours_is_tried_once_and_raised_at_once(error):
    call, state = flaky(99, error)
    waits = []

    with pytest.raises(type(error)):
        call_with_retry(call, sleep=waits.append)

    assert state["calls"] == 1
    assert waits == []


def test_a_first_try_that_works_never_waits():
    call, state = flaky(0, Boom(503))
    waits = []

    assert call_with_retry(call, sleep=waits.append) == "ok"
    assert state["calls"] == 1 and waits == []


def test_total_waiting_stays_short_so_a_coordinator_is_not_left_hanging():
    assert sum(RETRY_DELAYS_SECONDS) <= 5


def test_the_answer_budget_leaves_room_for_thinking():
    assert MAX_OUTPUT_TOKENS >= 4000


def test_the_reason_a_reply_was_empty_is_named_when_known():
    class Reason:
        name = "MAX_TOKENS"

    class Candidate:
        finish_reason = Reason()

    class Response:
        candidates = [Candidate()]

    assert describe_empty(Response()) == " (finish reason: MAX_TOKENS)"
    assert describe_empty(object()) == ""
    assert describe_empty(None) == ""


# ---------- through the real Gemini wrapper (with a fake library client) ----------
import json
from types import SimpleNamespace

from app import gemini_call
from app.llm import GeminiLLM, LLMError

from .test_gemini import GOOD, context


class ScriptedClient:
    """Fails with the given errors, one per call, then answers with a good plan."""

    def __init__(self, *errors):
        self.errors = list(errors)
        self.calls = 0
        self.models = SimpleNamespace(generate_content=self._generate)

    def _generate(self, **kwargs):
        self.calls += 1
        if self.errors:
            raise self.errors.pop(0)
        return SimpleNamespace(text=json.dumps(GOOD))


@pytest.fixture
def no_waiting(monkeypatch):
    monkeypatch.setattr(gemini_call, "RETRY_DELAYS_SECONDS", (0.0, 0.0))


@pytest.mark.usefixtures("no_waiting")
def test_the_planner_rides_out_two_busy_answers_from_google():
    client = ScriptedClient(Boom(503), Boom(503))

    plan = GeminiLLM("unused", "m", client=client).draft_plan(context())

    assert plan.recommended_urgency == "Critical"
    assert client.calls == 3


@pytest.mark.usefixtures("no_waiting")
def test_the_planner_gives_up_cleanly_if_google_stays_busy():
    client = ScriptedClient(Boom(503), Boom(503), Boom(503), Boom(503))

    with pytest.raises(LLMError, match="Gemini call failed"):
        GeminiLLM("unused", "m", client=client).draft_plan(context())

    assert client.calls == 3


@pytest.mark.usefixtures("no_waiting")
def test_the_planner_does_not_retry_a_wrong_key_or_model():
    client = ScriptedClient(Boom(403))

    with pytest.raises(LLMError):
        GeminiLLM("unused", "m", client=client).draft_plan(context())

    assert client.calls == 1


@pytest.mark.usefixtures("no_waiting")
def test_the_planner_asks_for_room_to_think():
    seen = {}

    class Spy(ScriptedClient):
        def _generate(self, **kwargs):
            seen.update(kwargs)
            return super()._generate(**kwargs)

    client = Spy()
    client.models = SimpleNamespace(generate_content=client._generate)
    GeminiLLM("unused", "m", client=client).draft_plan(context())

    assert seen["config"].max_output_tokens == MAX_OUTPUT_TOKENS


# ---------- the log must say which error it was ----------
from app.gemini_call import describe_error


def test_the_error_description_includes_the_http_number_and_status():
    err = Boom(429)
    err.status = "RESOURCE_EXHAUSTED"

    assert describe_error(err) == "Boom 429 RESOURCE_EXHAUSTED"
    assert describe_error(Boom(503)) == "Boom 503"
    assert describe_error(ValueError("x")) == "ValueError"


@pytest.mark.usefixtures("no_waiting")
def test_the_planner_error_message_names_the_http_number():
    client = ScriptedClient(Boom(403))

    with pytest.raises(LLMError, match=r"Gemini call failed: Boom 403"):
        GeminiLLM("unused", "m", client=client).draft_plan(context())


# ---------- a daily quota must not be retried ----------
from app.gemini_call import retry_after_seconds


class Quota(Boom):
    def __init__(self, message):
        super().__init__(429)
        self.message = message

    def __str__(self):
        return self.message


def test_a_daily_quota_is_not_transient():
    assert not is_transient(Quota("You exceeded your current quota. Please retry in 11h41m31.333652181s."))
    assert not is_transient(Quota("Please retry in 2m5s."))


def test_a_short_rate_limit_is_transient():
    assert is_transient(Quota("Please retry in 8.5s."))
    assert is_transient(Quota("Too many requests"))  # no wait given: try again briefly


def test_the_wait_google_asks_for_is_read_correctly():
    assert retry_after_seconds(Quota("Please retry in 11h41m31.5s.")) == pytest.approx(11 * 3600 + 41 * 60 + 31.5)
    assert retry_after_seconds(Quota("retry in 2m5s")) == 125
    assert retry_after_seconds(Quota("retry in 8.5s")) == 8.5
    assert retry_after_seconds(Quota("nothing useful")) is None


def test_a_daily_quota_is_raised_after_one_try_without_waiting():
    call, state = flaky(99, Quota("Please retry in 11h41m31.3s."))
    waits = []

    with pytest.raises(Quota):
        call_with_retry(call, sleep=waits.append)

    assert state["calls"] == 1 and waits == []
