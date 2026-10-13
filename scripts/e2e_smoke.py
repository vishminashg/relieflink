#!/usr/bin/env python3
"""
End-to-end smoke test of the real system: API + agent service + database (standard library only).

Start the API and the agent service first, then:
    python scripts/e2e_smoke.py --api http://localhost:5080 --password <Seed__DemoPassword>

It signs in as the seeded demo citizen and coordinator, files a relief request, starts the AI workflow and prints
what happened. Use --expect to say what you expect to see, for example when you start the agent service with a
MOCK_FAULT:   --expect PendingCoordinatorApproval   (default)   or   --expect FailedSafe
Then add --approve to carry on through approval and execution.
"""
import argparse
import json
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone


def call(method, url, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return resp.status, json.loads(resp.read() or b"null")
    except urllib.error.HTTPError as err:
        return err.code, json.loads(err.read() or b"null")


def login(api, email, password):
    status, body = call("POST", f"{api}/api/auth/login", body={"email": email, "password": password})
    if status != 200:
        sys.exit(f"Login failed for {email}: HTTP {status}")
    return body["token"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5080")
    ap.add_argument("--password", required=True, help="the Seed__DemoPassword you started the API with")
    ap.add_argument("--expect", default="PendingCoordinatorApproval")
    ap.add_argument("--approve", action="store_true")
    args = ap.parse_args()

    citizen = login(args.api, "citizen@relieflink.lk", args.password)
    coordinator = login(args.api, "coordinator@relieflink.lk", args.password)

    needed_by = (datetime.now(timezone.utc) + timedelta(hours=20)).isoformat()
    status, request = call("POST", f"{args.api}/api/relief-requests", citizen, {
        "householdSize": 6, "elderlyCount": 2, "childrenCount": 2, "homeFlooded": True,
        "district": "Kalutara", "addressText": "12 River Road, Kalutara", "latitude": 6.58, "longitude": 79.96,
        "notes": "Water is rising", "neededBy": needed_by,
        "items": [{"category": "Food", "quantity": 6}, {"category": "Water", "quantity": 12},
                  {"category": "Shelter", "quantity": 1}]})
    assert status == 201, (status, request)
    print(f"Request {request['id']}: triage score {request['triageScore']} -> {request['urgency']}")

    status, wf = call("POST", f"{args.api}/api/workflows", coordinator, {"reliefRequestId": request["id"]})
    assert status == 201, (status, wf)
    state = wf["workflow"]
    print(f"Workflow {state['id']}: {state['status']} (retries {state['retryCount']}, steps {len(wf['steps'])})")
    for step in wf["steps"]:
        tools = ", ".join(t["toolName"] for t in step["toolCalls"]) or "none"
        print(f"  step {step['sequence']} {step['agentName']}: {step['status']}; tools: {tools}"
              + (f"; error: {step['error']}" if step["error"] else ""))
    for v in wf["validations"]:
        print(f"  attempt {v['attempt']} {'PASS' if v['passed'] else 'FAIL'} {v['checkName']}: {v['detail']}")
    if state["failureReason"]:
        print(f"  failure reason: {state['failureReason']}")
    if wf["proposal"]:
        print("  proposal:", wf["proposal"]["summary"])

    if state["status"] != args.expect:
        sys.exit(f"UNEXPECTED: expected {args.expect}, got {state['status']}")
    print(f"OK: workflow ended in {args.expect}, as expected.")

    if args.approve and state["status"] == "PendingCoordinatorApproval":
        wid = state["id"]
        status, early = call("POST", f"{args.api}/api/workflows/{wid}/execute", coordinator)
        print(f"Execute before approval -> HTTP {status} (must be 409)")
        assert status == 409
        status, done = call("POST", f"{args.api}/api/workflows/{wid}/approve", coordinator, {"comment": "Smoke test"})
        assert status == 200, (status, done)
        print("Approved ->", done["workflow"]["status"])
        status, done = call("POST", f"{args.api}/api/workflows/{wid}/execute", coordinator)
        assert status == 200, (status, done)
        print("Executed ->", done["workflow"]["status"])
        status, req = call("GET", f"{args.api}/api/relief-requests/{request['id']}", coordinator)
        print("Request is now", req["status"])


if __name__ == "__main__":
    main()
