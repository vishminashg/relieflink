from datetime import datetime, timedelta, timezone

from app.config import Settings

KEY = "test-service-key-0123456789"
NOW = datetime(2026, 10, 14, 8, 0, tzinfo=timezone.utc)


def payload(**changes):
    """The golden request: household of 6, flooded, Food + Water + Shelter, triage 71 = Critical."""
    body = {
        "requestId": "11111111-1111-1111-1111-111111111111",
        "district": "Kalutara",
        "householdSize": 6, "elderlyCount": 2, "childrenCount": 2, "homeFlooded": True,
        "neededBy": (NOW + timedelta(hours=20)).isoformat(),
        "triageScore": 71, "urgency": "Critical",
        "triageReasons": ["Household of 6 adds 9 points", "Home is flooded adds 10 points"],
        "needs": [
            {"category": "Food", "quantity": 6},
            {"category": "Water", "quantity": 12},
            {"category": "Shelter", "quantity": 1},
        ],
        "notes": "Water is rising",
        "attempt": 1,
        "feedback": None,
    }
    body.update(changes)
    return body


def settings(**changes) -> Settings:
    base = dict(service_key=KEY)
    base.update(changes)
    return Settings(**base)
