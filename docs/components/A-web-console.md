# Component A: coordinator web console

Owner: Gurusingha V S H (group 2026-PR-01). Backend and agent: see [A-relief-requests.md](A-relief-requests.md) and [A-workflow-and-planner-agent.md](A-workflow-and-planner-agent.md).

## Purpose
A coordinator needs to see which households need help first, understand why, and decide whether an AI-drafted plan goes ahead. The console exists for those three jobs and nothing else.

## Screens
| Route | Job |
|---|---|
| `/login` | Sign in. Citizens and volunteers are refused and no token is kept. |
| `/` | Overview: critical-open count, most urgent requests, breakdown by status, urgency and district. |
| `/requests` | Queue sorted most urgent first, then longest waiting. Filters: status, urgency, district, order, text search. Paged. |
| `/requests/:id` | Needs, notes, household facts, the reasons behind the triage score, urgency override (reason required), cancel (reason required), start an AI plan. |
| `/workflows` | Plans by status. Defaults to plans waiting for a decision. |
| `/workflows/:id` | Progress path, the plan, safety checks grouped by draft, decisions, audit trail of agent steps and tool lookups, decision panel. |

## Rules the UI follows
- The human decides. Approve, ask for a new draft and reject are only offered while the workflow waits for a coordinator; execute only after approval. The API enforces the same rules; the UI only avoids offering impossible actions (`src/lib/workflow.ts`).
- Revise and reject need a comment of at least 3 characters, the same rule as the API.
- Safety checks are shown per draft, so a coordinator can see that an earlier draft failed and why.
- Errors show the API's own message. A failed action reloads the real state (for example after a 409 conflict).
- The token lives in `sessionStorage` (cleared when the tab closes). A 401 on a signed-in call ends the session. A failed login keeps the server's message ("Invalid email or password.").
- The API address comes from `VITE_API_URL`. No secrets exist in the front end.

## Design choices
- Urgency is the first thing the eye should find: a coloured spine and a large score on every queue row, with the level written in words so colour is never the only signal.
- Typefaces: Atkinson Hyperlegible (made for legibility under stress) for text, Archivo (variable width) for headings and numbers. Self-hosted through `@fontsource`, so no external font requests.
- Keyboard focus is always visible, there is a skip link, tables have captions, check results are readable by screen readers, and motion is limited.
- The workflow path is numbered because it really is a sequence. Nothing else is numbered.

## Tests (63, Vitest + Testing Library)
| File | What it proves |
|---|---|
| `lib/workflow.test.ts` | Which buttons exist in each workflow status; path states; labels |
| `lib/format.test.ts` | Waiting and deadline wording; 25 distinct districts |
| `api/client.test.ts` | Bearer token, empty filters dropped, error mapping, 401 handling, network failure |
| `pages/Login.test.tsx` | Guard redirect, staff sign in, citizen and volunteer refused with no token kept, wrong password message |
| `pages/Requests.test.tsx` | Rows, default sort, filters sent to the API, search on submit, empty state, retry |
| `pages/RequestDetail.test.tsx` | Override and cancel rules, start plan, closed requests are read-only, server errors shown |
| `pages/WorkflowDetail.test.tsx` | Button states for every status, approve/revise/reject/execute calls, conflict reload, evidence panels |
| `pages/Workflows.test.tsx` | Default filter, all plans, sign out |

Mutation spot checks (break the code, the tests must fail): allow execute while Proposed; allow revise without comment; keep a token for a citizen; fire the unauthorized handler without a token; change the default sort. All five were caught.

Also checked by hand against the real API and agent in a browser: sign in, draft, approve, execute, revise, reject, citizen refusal, mobile width with no horizontal scroll.

## Viva questions you should be able to answer
1. **Why can a coordinator not press Execute on a plan that is still waiting?** The UI hides it (`availableActions`), and the API refuses it anyway with a conflict. The UI is a convenience, the API is the rule.
2. **What happens if two coordinators act on the same plan?** The second request fails with 409. The page shows the message and reloads the real state.
3. **Why `sessionStorage` and not `localStorage`?** The token disappears when the tab closes, which limits exposure on a shared computer. Trade-off: the coordinator signs in again after closing the tab.
4. **Why does the console refuse citizens when the API already would?** A citizen's token would have no use here, and keeping it would be needless risk. Authorization is still enforced by the API on every call.
5. **How do you stop an old slow response overwriting a newer one?** `useLoad` numbers each request and ignores answers from older ones.
6. **Why show failed drafts to the coordinator?** Trust. They can see the agent was corrected by fixed rules, not the AI marking its own work.
7. **How would you add a new filter?** Add the field to `RequestFilters`, a control in `Requests.tsx`, and a test that the query string contains it.

## Screenshots
Taken from the running console against the real API and agent (mock mode): [overview](../screenshots/web-overview.png), [request queue](../screenshots/web-requests.png), [plan awaiting decision](../screenshots/web-plan-decision.png), [queue at phone width](../screenshots/web-requests-mobile.png).
