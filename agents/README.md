# ReliefLink agent service

Python FastAPI + LangGraph service that hosts the AI agents. It is **internal**: only the ASP.NET Core API calls it,
using a shared service key. It stores nothing and cannot reach the database.

Currently implemented: the **Planner agent** (Component A). Other components add their own agents in `app/`.

## Planner agent in one picture
```
gather (tools) -> draft (model) -> check (self-check) -> finalize
                     ^                  |
                     +---- problems ----+   (at most 2 drafts)
```
* **Tools** (`app/tools.py`) are a fixed allow-list of four read-only functions. Each call is recorded and returned to the API for the audit trail.
* **Model** (`app/llm.py`): `mock` (default, deterministic, needs no key) or `gemini`.
* The API validates the plan again with ordinary C# code. The agent's self-check only saves round trips.

## Run locally
```bash
cd agents
python -m venv .venv && source .venv/bin/activate        # Windows: .venv\Scripts\activate
pip install -r requirements-dev.txt

export AGENT_SERVICE_KEY="$(openssl rand -hex 24)"       # same value goes into the API as Agent__ServiceKey
export LLM_MODE=mock                                      # no API key needed
uvicorn app.main:app_factory --factory --port 8001
```
Check it: `curl http://localhost:8001/health`

Point the API at it:
```bash
export Agent__BaseUrl=http://localhost:8001
export Agent__ServiceKey=<the same value as AGENT_SERVICE_KEY>
```

## Settings (environment variables only, never commit real values)
| Variable | Meaning |
|---|---|
| `AGENT_SERVICE_KEY` | Required, at least 16 characters. Callers must send it as `X-Service-Key`. |
| `LLM_MODE` | `mock` (default) or `gemini` |
| `GEMINI_API_KEY` | Required when `LLM_MODE=gemini`. Create one at https://aistudio.google.com |
| `GEMINI_MODEL` | Model name, default `gemini-2.5-flash`. Check AI Studio for the current free model name. |
| `LLM_TIMEOUT_SECONDS` | Default 20 |
| `MOCK_FAULT` | Demo and testing only: `none` (default), `drop_need_first_attempt`, `lower_urgency` |

`MOCK_FAULT` makes the mock agent misbehave on purpose so you can show the API's validation, retry and fail-safe behaviour
in the demo video. Never set it in production.

## Run the tests
```bash
cd agents
pytest
```
The tests never call a real model. The Gemini path is tested with a fake client (prompt building, schema parsing, every kind of failure).

## Contract with the API
`POST /plan` with header `X-Service-Key`. Request and response are camelCase JSON, defined in `app/schemas.py`
and mirrored by `Domain/ReliefPlan.cs` in the API.
