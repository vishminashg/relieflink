# Component A: Relief Request Management

Owner: Student 1 (Gurusingha V S H) · Branch: `feature/requests`

## 1. What this component does
A citizen submits a request for relief: how many people, who is vulnerable, what they need, where they are and by when. The system checks the request, gives it an **urgency score from 0 to 100** using fixed rules (no AI), and keeps it through its lifecycle. Coordinators see the most urgent requests first and can override the urgency with a written reason. Citizens can only ever see their own requests.

## 2. Files and what each one does
| File | Purpose |
|---|---|
| `Domain/ReliefRequest.cs` | The `ReliefRequest` and `ReliefRequestItem` entities and their enums (status, urgency, need category) |
| `Domain/TriageScorer.cs` | Pure function that turns a request into a score, a level and plain-language reasons |
| `Domain/RequestStatusRules.cs` | The only legal status changes and who can edit, cancel or re-triage |
| `Domain/SriLanka.cs` | The 25 districts and the coordinate box that contains Sri Lanka |
| `Services/ReliefRequestValidator.cs` | Every input rule in one place, returns all errors at once |
| `Controllers/ReliefRequestsController.cs` | The seven endpoints |
| `Controllers/Dtos/ReliefRequestDtos.cs` | What goes in and out of the API |
| `Data/Configurations/ReliefRequestConfiguration.cs` | Table mapping, check constraints, indexes, foreign keys |
| `Data/AppDbContext.Requests.cs` | Registers this component's tables (a partial class, so each component has its own file) |
| `Data/Migrations/*AddReliefRequests*` | The generated SQL that creates the tables |
| `Auth/Roles.cs` | Role names used in `[Authorize(Roles = ...)]` |

## 3. Endpoints
| Method | Path | Who | What it does |
|---|---|---|---|
| POST | `/api/relief-requests` | Citizen | Create a request. Score, urgency and status are set by the server |
| GET | `/api/relief-requests` | Citizen (own only), Coordinator, Admin | List with filters (status, urgency, district, search), sort, paging |
| GET | `/api/relief-requests/{id}` | Owner, Coordinator, Admin | One request with its needs |
| PUT | `/api/relief-requests/{id}` | Owner | Edit, only while status is `Submitted`. Score is recomputed |
| POST | `/api/relief-requests/{id}/cancel` | Owner, Coordinator, Admin | Cancel while still waiting for a decision |
| POST | `/api/relief-requests/{id}/triage` | Coordinator, Admin | Override urgency (reason required) or recompute from the rules |
| GET | `/api/relief-requests/summary` | Coordinator, Admin | Counts by status, urgency and district for the dashboard |

## 4. Business rules
**Urgency score (out of 100)**

| Factor | Points |
|---|---|
| Household size | 1.5 per person, max 15 |
| Vulnerable members | 5 per elderly person + 4 per child, max 25 |
| Home flooded | 10 |
| Needs (Medical 15, Shelter 10, Water 8, Food 6, Sanitation 3, Clothing 2) | max 30 |
| Deadline | within 12h = 15, within 24h = 10, within 48h = 5 |
| Waiting time | 1 per 3 hours, max 5 |

Levels: **Critical** 70+, **High** 50+, **Medium** 30+, otherwise **Low**.
Worked example: family of 6, 2 elderly, 2 children, flooded home, needs food, water and shelter within 24 hours = 9 + 18 + 10 + 24 + 10 = **71, Critical**.

**Lifecycle:** `Submitted → UnderReview → PlanReady → Approved → InProgress → Fulfilled`. A request can be `Cancelled` or `Rejected` only while it is `Submitted`, `UnderReview` or `PlanReady`. Only `Submitted` requests can be edited.

**Override rule:** a coordinator can set urgency by hand, but must give a reason (3 to 500 characters). Later edits keep the manual urgency but still recompute the score. Recomputing with no body clears the override.

## 5. Security decisions
- The client can never set status, score, urgency or the owner. Those fields do not exist on the input type, and the server sets them.
- A citizen asking for someone else's request gets **404, not 403**, so the existence of the request is not revealed.
- Volunteers cannot list requests at all, because they contain home locations.
- Every create, update, cancel and triage action writes an audit log row in the same save as the change.
- The database enforces the important limits itself with check constraints, so a bug in the code cannot store impossible data.

## 6. Database
Tables: `ReliefRequests` and `ReliefRequestItems` (one line per need).
Constraints: household 1 to 50, elderly + children never more than the household, score 0 to 100, quantity 1 to 1000, one line per category per request.
Foreign keys: request → requester (restrict), item → request (cascade), workflow → request (restrict, so a request with a workflow cannot be deleted).
Indexes: status, urgency, district, requester, created date.
Enums are stored as text so the database stays readable and reordering an enum in code cannot corrupt old rows.

## 7. Tests (158 new tests, all passing)
- `TriageScorerTests`: golden case, every cap, every deadline and waiting boundary, level thresholds, determinism.
- `ReliefRequestValidatorTests`: every rule, and that all errors come back together.
- `RequestStatusRulesTests`: legal steps, skipped steps, closed statuses, edit and cancel rules.
- `ReliefRequestsTests` (integration, real PostgreSQL): roles, ownership, filters, sorting, paging, edit, cancel, triage, summary, audit rows, and database constraints.

## 8. Likely viva questions
1. **Why is urgency calculated in code and not by AI?** The same request must always get the same score, a coordinator must be able to see why, and it must be testable. The AI planner later uses the score and reasons as evidence.
2. **How do you stop one citizen reading another's request?** The list filters by the owner from the token, and `CanSee` checks ownership on get, update and cancel. Strangers get 404. Tests: `Get_AnotherCitizensRequest_Returns404...`, `List_Citizen_SeesOnlyTheirOwnRequests`.
3. **Why can't the client send a status or score?** `ReliefRequestInput` has no such properties, so extra JSON is ignored. Test: `Create_ClientCannotSetStatusScoreUrgencyOrRequester`.
4. **Why have database constraints when the code validates?** Defence in depth: the database also protects against bugs and other writers. Tests: `Database_RejectsImpossibleRequests`.
5. **Why return 409 when editing after review started?** The request exists but its current state does not allow the action.
6. **What bug did the tests find?** Editing a request with a new need returned 500. New items already had an Id, so EF assumed the row existed and tried to UPDATE it. Fixed by adding new items explicitly with `db.ReliefRequestItems.Add`.
7. **Why a partial `AppDbContext` and separate configuration classes?** Five students add tables in parallel, so each works in their own file and merge conflicts are rare.
8. **What happens to an override when the citizen edits?** The manual urgency stays, the score is recomputed.
9. **Why 404 for a missing request but 403 for a volunteer on the list?** The volunteer role is simply not allowed on that endpoint. A missing or foreign request is hidden.
10. **What would you add with more time?** Photo upload (today it is an https link), optimistic concurrency on edits, and rate limiting on request creation.

## 9. Changes you should be able to make live (practise each one)
1. Change the Critical threshold from 70 to 65 (`TriageScorer.CriticalThreshold`), then fix the tests that fail.
2. Add a new need category `Transport` with weight 4 (enum, weight table, one test).
3. Limit household size to 30 (validator constant, check constraint, new migration).
4. Change the default page size from 20 to 10.
5. Add a `ContactPhone` field to requests (entity, DTO, controller mapping, migration, test).

## 10. Known limitations (be honest about these)
- Photos are accepted only as an https link, not uploaded files.
- No optimistic concurrency: two simultaneous edits, the last one wins.
- The triage weights are a documented judgement, not derived from data.
- Starting the AI workflow from a request is built next, in the workflow part of this component.
