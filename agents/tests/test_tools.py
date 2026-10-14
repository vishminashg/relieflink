from datetime import datetime, timedelta, timezone

import pytest

from app.tools import ALLOWED_TOOLS, ToolBox, ToolNotAllowed, deadline_pressure, need_priority_order, \
    triage_breakdown, vulnerability_summary

NOW = datetime(2026, 10, 14, 8, 0, tzinfo=timezone.utc)


def test_allow_list_is_exactly_these_read_only_tools():
    assert set(ALLOWED_TOOLS) == {
        "triage_breakdown",
        "need_priority_order",
        "vulnerability_summary",
        "deadline_pressure",
        "find_volunteers",
        "volunteer_roster_summary",
    }



def test_tool_outside_the_allow_list_is_refused_and_not_recorded():
    box = ToolBox()
    for name in ["delete_all_requests", "send_email", "run_sql", "__import__", ""]:
        with pytest.raises(ToolNotAllowed):
            box.call(name)
    assert box.records == []


def test_every_call_is_recorded_with_arguments_result_and_time():
    box = ToolBox()

    box.call("triage_breakdown", score=71, urgency="Critical", reasons=["a"])

    record = box.records[0]
    assert record.name == "triage_breakdown"
    assert record.arguments == {"score": 71, "urgency": "Critical", "reasons": ["a"]}
    assert record.result["score"] == 71
    assert record.succeeded is True
    assert record.duration_ms is not None and record.duration_ms >= 0


def test_a_failing_tool_is_recorded_as_failed_and_reraised():
    box = ToolBox({"boom": lambda: 1 / 0})

    with pytest.raises(ZeroDivisionError):
        box.call("boom")

    assert box.records[0].succeeded is False
    assert "division" in box.records[0].result["error"]


def test_triage_breakdown_reports_distance_to_critical():
    assert triage_breakdown(71, "Critical", [])["pointsToCritical"] == 0
    assert triage_breakdown(40, "Medium", ["x"])["pointsToCritical"] == 30


def test_triage_breakdown_limits_the_reasons_it_returns():
    assert len(triage_breakdown(10, "Low", [str(i) for i in range(50)])["reasons"]) == 10


def test_need_priority_puts_life_critical_needs_first_and_is_stable():
    needs = [{"category": "Clothing", "quantity": 1}, {"category": "Food", "quantity": 2},
             {"category": "Medical", "quantity": 1}, {"category": "Shelter", "quantity": 1}]

    order = [o["category"] for o in need_priority_order(needs)["order"]]

    assert order == ["Medical", "Shelter", "Food", "Clothing"]
    assert [o["rank"] for o in need_priority_order(needs)["order"]] == [1, 2, 3, 4]


def test_vulnerability_summary():
    result = vulnerability_summary(household=6, elderly=2, children=2, flooded=True)

    assert result["vulnerableCount"] == 4
    assert result["vulnerableShare"] == 0.67
    assert len(result["flags"]) == 3
    assert vulnerability_summary(2, 0, 0, False)["flags"] == []


@pytest.mark.parametrize("hours,band", [(-1, "overdue"), (0, "overdue"), (5, "under12"), (12, "under12"),
                                         (13, "under24"), (24, "under24"), (30, "under48"), (48, "under48"), (49, "later")])
def test_deadline_bands(hours, band):
    result = deadline_pressure(NOW + timedelta(hours=hours), NOW)

    assert result["band"] == band


def test_no_deadline():
    assert deadline_pressure(None, NOW) == {"hoursRemaining": None, "band": "none"}


def test_a_deadline_without_timezone_is_treated_as_utc():
    naive = datetime(2026, 10, 14, 10, 0)

    assert deadline_pressure(naive, NOW)["hoursRemaining"] == 2.0
