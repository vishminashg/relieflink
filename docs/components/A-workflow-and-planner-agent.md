# Component A (part 2): Relief Workflow and Planner Agent

Owner: Student 1 (Gurusingha V S H) · Branch: `feature/workflow`
Part 1 (requests, triage score) is described in [A-relief-requests.md](A-relief-requests.md).

## 1. What this adds
A coordinator selects a submitted relief request and starts a **workflow**. An AI **Planner agent** proposes a plan
(what to send, how fast, why). Ordinary code **validates** the plan. A **human coordinator** then approves it, rejects it,
or asks for a revision. Only after approval can the plan be executed.

```
Coordinator starts workflow
        |
        v
  Planning ---> Proposed ---> Validating ---> PendingCoordinatorApproval ---> Approved ---> Executed
     ^                            |                    |   |
     |   checks failed, retry     |                    |   +--> Rejected   (request goes back to Submitted, or is rejected)
     +----------------------------+                    +------> RevisionRequested --> Planning  (max 2 revisions)

Anything unrecoverable (agent offline, 3 bad plans in a row) --> FailedSafe, and the request returns to Submitted
```

## 2. Files and what each one does
| File | Purpose |
|---|---|
| `Domain/ReliefPlan.cs` | The records exchanged with the agent service (input, plan, tool calls) |
| `Domain/PlanValidator.cs` | **Deterministic** safety checks on a plan. Pure function, no AI |
| `Services/PlannerClient.cs` | `IPlannerClient` and the HTTP client that calls the agent service (timeout, size limit, service key) |
| `Services/WorkflowOrchestrator.cs` | Runs the workflow: planning loop, retries, fail-safe, approve, reject, revise, execute |
| `Controllers/WorkflowsController.cs` | The seven endpoints |
| `Controllers/Dtos/WorkflowDtos.cs` | What the API returns (plan, steps, tool calls, validation results, decisions) |
| `Data/Configurations/AgentWorkflowConfiguration.cs` | Unique index: one live workflow per request |
| `Data/Migrations/*AddWorkflowEngine*` | `ValidationResults.Attempt`, `ToolCalls.Sequence`, the unique index |
| `agents/app/planner.py` | The LangGraph graph (gather, draft, check, finalize) |
| `agents/app/tools.py` | The four allow-listed read-only tools and the call recorder |
| `agents/app/llm.py` | `MockLLM` (default) and `GeminiLLM` |
| `agents/app/main.py`, `security.py`, `config.py`, `schemas.py` | FastAPI app, service-key check, settings, wire format |

## 3. Endpoints (all Coordinator or Admin)
| Method | Path | What it does |
|---|---|---|
| POST | `/api/workflows` | Start a workflow for a Submitted request. Runs the first planning cycle |
| GET | `/api/workflows` | List (filter by status or request, paging, newest first) |
| GET | `/api/workflows/{id}` | Full detail: plan, steps, tool calls, validation results per attempt, decisions |
| POST | `/api/workflows/{id}/approve` | Approve the plan (comment optional) |
| POST | `/api/workflows/{id}/reject` | Reject the plan (comment required). `rejectRequest: true` also rejects the request |
| POST | `/api/workflows/{id}/revise` | Ask the agent for a new plan (comment required, at most 2 times) |
| POST | `/api/workflows/{id}/execute` | Hand an **approved** plan to the field. 409 unless approved |

Status codes you should know: 409 when the action is not allowed in the current state, 404 for an unknown id, 403 for citizens and volunteers, 401 without a token.

## 4. The validation checks (`PlanValidator`)
All eight always run, so every problem is reported together and sent back to the agent as feedback.

| Check | Fails when |
|---|---|
| `plan-present` | The agent returned no plan |
| `needs-covered` | A requested need has no plan line |
| `no-invented-needs` | The plan has a need that was not requested, an unknown category (including a number), or a repeated need |
| `quantities-within-request` | A quantity is below 1 or above what the family asked for |
| `urgency-not-downgraded` | Recommended urgency is lower than the triage result, or is not a real level (a number such as "1" is refused too) |
| `response-time-matches-urgency` | Promised hours are not between 1 and the limit: Critical 6, High 12, Medium 24, Low 72. The **stricter** of the recommended and triage level applies |
| `text-present-and-bounded` | Summary missing, too short or over 1000 characters; no rationale; too many items; a blank action |
| `no-links-or-markup` | Any text contains `http://`, `https://`, `<` or `>` |

## 5. Security and safety decisions
- **A citizen can try to hijack the agent through the free-text notes** (prompt injection). Defences, in layers:
  1. The notes are passed as data inside a fenced block, and the instructions tell the model never to follow them.
  2. The fence cannot be closed early by the notes, and the notes are cut to 1000 characters.
  3. The mock agent never reads the notes at all.
  4. **Whatever the model returns is re-checked by `PlanValidator`.** If a hijacked agent says "urgency Low, respond in 72 hours", the checks fail. This layer works no matter how the model behaves. Test: `InjectedNotes_CannotLowerTheUrgency`.
- **The agent cannot skip the human.** Every status change goes through `WorkflowStateMachine`. Execute is only possible from Approved. Test: `Execute_BeforeApproval_Returns409_AndNothingChanges`.
- **The agent service is internal.** The API sends a shared secret in `X-Service-Key`; the agent compares it in constant time and refuses to start without one. It has no database access and no network tools.
- **Tools are an allow-list of four read-only functions.** A tool name outside the list is refused and never recorded.
- **Everything coming back from the agent service is untrusted:** response size is capped at 256 KB, at most 20 tool calls are kept, and each stored tool result is limited to 4000 characters.
- **Failure is safe, not silent.** If the agent is offline or keeps producing bad plans, the workflow ends as `FailedSafe` with the reason, and the request returns to `Submitted`. Nothing reaches the coordinator.
- **Audit:** started, proposal ready, approved, rejected, revision requested, executed and failed-safe are each written to the audit log in the same save as the change.
- **One live workflow per request,** enforced twice: a check in the controller and a unique database index. Test: two simultaneous starts, exactly one wins.
- The agent's own text is shown as plain text and links are blocked, so a plan cannot carry a phishing link to a coordinator.

## 6. How the Planner agent works
`gather` calls four tools (triage breakdown, need priority order, vulnerability summary, deadline pressure) and records each call.
`draft` asks the model (or the mock) for a plan using those facts plus any earlier problems and the coordinator's comment.
`check` runs the agent's own quick self-check with the same key rules; if it finds problems and fewer than 2 drafts were made, it loops back to `draft`.
`finalize` strips markup and links and returns the plan plus the tool calls.
The self-check only saves round trips. The API's validator is the authority.

Model choice: `LLM_MODE=mock` (default; deterministic, free, used by all tests and CI) or `LLM_MODE=gemini` (needs `GEMINI_API_KEY`).

## 7. Tests
- `PlanValidatorTests` (52): every check, every boundary, both directions.
- `HttpPlannerClientTests` (17): good reply, service key and body format, not configured, bad status codes, malformed JSON, oversized reply, network failure, cancellation.
- `WorkflowsTests` (43, real PostgreSQL, fake agent): roles, every state transition, retries with feedback, fail-safe, outage, prompt injection, link in output, tool-call bounds and order, approve/reject/revise/execute rules, citizen cancelling mid-way, two simultaneous starts, database index.
- Agent service (`pytest`, 95 tests): tools and allow-list, planner graph, self-check loop, text cleaning, API authentication and input validation, start-up safety, Gemini prompt and parsing with a fake client.
- Mutation spot-checks (I broke the code on purpose and confirmed the tests fail): executing without approval, allowing urgency downgrade, dropping the comment requirement, never stopping retries, skipping the PlanReady transition, an unprotected allow-list, no service key check, no text cleaning.
- End-to-end with the real API, real database and real agent service over HTTP (`scripts/e2e_smoke.py`): healthy run, agent drops a need then recovers, agent lowers urgency until fail-safe, agent offline.

## 8. Showing it in the demo (no Gemini key needed)
Start the agent service with a fault, then run the same workflow:
| Start the agent with | You will see |
|---|---|
| nothing special | One attempt, 8 checks pass, plan waits for approval |
| `MOCK_FAULT=drop_need_first_attempt` | Attempt 1 fails `needs-covered`, attempt 2 passes, plan waits for approval |
| `MOCK_FAULT=lower_urgency` | Three attempts all fail, the workflow ends `FailedSafe` |
| the agent stopped | Three failed steps ("could not be reached"), `FailedSafe` |

## 9. Likely viva questions
1. **Why does a plain C# validator check the AI's output when the AI is supposed to be smart?** A language model can be wrong or be manipulated. The validator is ordinary code with fixed rules, so the safety of the system does not depend on the model behaving.
2. **What is prompt injection and how did you handle it?** A citizen writes instructions in the notes field hoping the model obeys. I fence the notes as data, and, more importantly, I validate the output so an obeyed instruction (lower urgency, slower response) cannot pass. Test: `InjectedNotes_CannotLowerTheUrgency`.
3. **Where is the human-in-the-loop gate?** `PendingCoordinatorApproval`. The only way out to `Approved` is `ApproveAsync`, called by a Coordinator or Admin. Execute is refused unless approved (409).
4. **What happens if the agent service is down?** Each of 3 attempts fails, the workflow becomes `FailedSafe`, the request goes back to `Submitted`, and the reason is shown. Test: `Start_WhenTheAgentIsDown_FailsSafe...`.
5. **Why retry, and why only twice?** Models give a bad answer sometimes, and the validator's findings are sent back as feedback. A cap stops endless loops and cost. `MaxRetries = 2`, `MaxRevisions = 2`.
6. **Why does the response-time check use the stricter urgency?** Otherwise an agent could lower the urgency and thereby earn a longer allowed response time. I found this while writing the integration test for injection.
7. **Why are tools an allow-list?** The agent can only ever do the four read-only things we gave it. A model asking for `delete_all_requests` is refused.
8. **Why does the API call the agent service instead of the browser calling it?** The service key must stay secret, the API enforces roles, and the agent has no access to data it was not given.
9. **How do you stop two coordinators starting a workflow for the same request?** A check in the controller plus a unique partial index in PostgreSQL. If both race, one gets 409.
10. **Why is the plan stored as JSON?** The plan structure will evolve, the API treats it as a document, and PostgreSQL `jsonb` keeps it queryable.
11. **Is the agent deterministic?** The mock is. The Gemini model is not, which is why the tests use the mock and why validation exists.
12. **What bug did testing find?** The first version let a "lowered" urgency choose its own slower time limit. The injection test exposed it, and the fix is in `PlanValidator` (stricter level wins).
13. **How is the Gemini mode tested without a key?** With a fake client: prompt building, the fenced notes, schema parsing and every failure type. A live call is verified manually once a key is set.

## 10. Changes you should be able to make live (practise each one)
1. Change the Critical response limit from 6 to 4 hours (`PlanValidator.MaxResponseHours`, the agent's `RESPONSE_LIMIT_HOURS`, and the tests).
2. Allow one more revision (`WorkflowStateMachine.MaxRevisions`) and fix the test that names the limit.
3. Add a ninth check, for example "the summary must mention the district", with a unit test.
4. Add a fifth tool to the agent (e.g. `household_density`) to the allow-list, call it in `gather`, add a test.
5. Make `reject` always require `rejectRequest` to be set explicitly (decide and test).

## 11. Known limitations (be honest about these)
- The workflow runs inside one HTTP request. A slow model makes the coordinator wait, up to roughly 20 seconds per attempt. A background job queue would be the next step.
- If the API process dies in the middle of a run, the workflow stays in `Planning`. A recovery sweep is not built.
- No optimistic concurrency on approvals: two coordinators clicking at the same instant is guarded by the state check, but not by a row version.
- Only the Planner agent exists so far. The Supply, Volunteer Matching, Logistics and Safety agents come with Components B to E.
- Weather data is not used yet.
- The Gemini path has been tested only against a fake client here. It needs one real run with a key. The agent's Docker image has not been built in my environment.
- The agent's self-check repeats a few of the backend's rules in Python on purpose; they must be kept in step.
