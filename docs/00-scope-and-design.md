# ReliefLink — Disaster Relief Coordination Platform
SE3090 Assignment 1 · Scope and Design Baseline · v0.1 (working name, rename freely)

## 1. Real-world problem
During floods and landslides in Sri Lanka (Kalutara, Gampaha, Ratnapura, Kegalle), relief is coordinated through phone calls, WhatsApp groups and spreadsheets. Families in need are missed or duplicated, donated supplies sit in the wrong depot, volunteers are double-booked, and shelters exceed capacity.

ReliefLink gives **affected families and volunteers a Flutter mobile app** and **relief coordinators a React web console**. An Agentic AI workflow turns each relief request into a validated, evidence-backed relief plan (supplies, volunteers, delivery slot, shelter) that a **coordinator must approve** before any stock is reserved or anyone is dispatched.

## 2. User roles
| Role | Primary client | Responsibilities |
|---|---|---|
| Citizen (affected family) | Flutter | Register, submit relief requests with GPS and photo, track status |
| Volunteer | Flutter | Set availability, view assignments, update delivery status |
| Relief Coordinator | React | Triage, review AI plans, approve / reject / revise, dashboards |
| Administrator | React | Manage users, roles, depots, vehicles, system settings |

User management and JWT auth are a shared mandatory feature, not one of the five business components.

## 3. Five business components (one per student)
Final owner names are filled in by the group (swap freely before coding starts).

| # | Component | Student owner | Business-specific operation (beyond CRUD) | Owned agent |
|---|---|---|---|---|
| A | Relief Request Management | Student 1 | Urgency triage scoring; starts the agent workflow | Planner / Triage Agent |
| B | Supply and Inventory Management | Student 2 | Transactional stock reservation, never negative stock | Supply Agent |
| C | Volunteer Management | Student 3 | Skill + availability matching, no double-booking | Volunteer Matching Agent |
| D | Distribution and Scheduling | Student 4 | Conflict-free delivery slot + route weather risk | Logistics Agent |
| E | Shelter and Capacity Management | Student 5 | Capacity-enforced check-in (transaction) | Safety and Validation Agent |

Each student delivers, for their own component: ASP.NET Core endpoints, EF Core entities and migrations, React screens, Flutter screens, tests, documentation, and one distinct agent.

Student 1 additionally owns the shared auth endpoints and the workflow endpoints (start, status, approve, reject, revise, summary). This is a technical ownership, not a management role.

## 4. Architecture
```
Flutter app ─┐                                ┌─ PostgreSQL (EF Core, migrations)
             ├─ HTTPS/REST/JSON ─ ASP.NET Core Web API ─┤
React web ───┘     (JWT, RBAC, validation,    ├─ Agent service (Python FastAPI + LangGraph, internal only)
                    business rules, audit,     └─ Third party: Open-Meteo weather (via API only)
                    workflow state, approval)
```
Rules enforced by design:
- React and Flutter talk **only** to ASP.NET Core.
- The Python agent service is internal, called only by ASP.NET Core, with a shared service key.
- ASP.NET Core persists all workflow state; the agent service is stateless per call.
- Agents may call only allow-listed read-only tools. Writes (reserve stock, create assignment) happen in ASP.NET Core after human approval.

## 5. Agentic AI design
### 5.1 Distinct agents
| Agent | Responsibility | Input contract | Output contract | Allowed tools (read-only) |
|---|---|---|---|---|
| Planner / Triage | Turn the request into a structured multi-step plan, assign steps | `ReliefRequestDto` | `Plan{steps[{id, agent, goal}]}` | none |
| Supply | Propose items and quantities from available depot stock | plan step + request needs | `SupplyProposal{lines[{itemId, depotId, qty}]}` | `check_stock`, `list_depots` |
| Volunteer Matching | Propose volunteers with required skills and availability | plan step + date window | `VolunteerProposal{volunteerIds[]}` | `find_volunteers` |
| Logistics | Propose delivery slot, vehicle and route risk | supply + volunteer proposals | `LogisticsProposal{slot, vehicleId, weatherRisk}` | `find_delivery_slot`, `get_weather_risk` |
| Safety and Validation | Screen for prompt injection, check shelter capacity, assemble final proposal for deterministic validation | all proposals | `ValidatedProposal{checks[], shelterId, ok}` | `get_shelter_capacity` |

Each agent has its own prompt, schema, tool permissions and a visible step in the workflow trace. Renamed copies of one prompt do not count.

### 5.2 Minimum assessed workflow (golden path)
Objective: "Family of 6 in Kalutara, 2 elderly, flooded home. Need food, drinking water and shelter within 24 hours."

1. Flutter: citizen logs in, submits request (household, needs, GPS, photo).
2. ASP.NET Core: validates JWT, role, input; saves request `Submitted`; computes triage score; creates workflow row.
3. Planner Agent creates the structured plan and delegates steps.
4. Supply, Volunteer, Logistics agents each run via allow-listed tools and return structured output.
5. Safety and Validation Agent screens inputs and checks shelter capacity.
6. **Deterministic validation (C#)**: JSON schema; items/volunteers/vehicles exist; stock sufficient; skills match; no schedule overlap; per-household quantity limits; shelter capacity; weather risk under threshold or flagged.
7. Status becomes `PendingCoordinatorApproval`. React shows plan, evidence, validation results and trace.
8. Coordinator approves, rejects, or requests revision (max 2 revision loops).
9. On approval, one database transaction: reserve stock, create assignment, create delivery run, reserve shelter bed.
10. Flutter shows the updated status to the citizen.

Failure path: if validation still fails after the retry limit, status becomes `FailedSafe` with recorded reasons. No stock is reserved. A prompt-injection attempt in free-text notes is flagged and rejected.

### 5.3 Workflow state machine
`Submitted → Planning → Proposed → Validating → PendingCoordinatorApproval → Approved → Executed`
Side exits: `RevisionRequested → Planning`, `Rejected`, `FailedSafe`.

### 5.4 Persistence and observability
Tables: `AgentWorkflows`, `WorkflowSteps`, `ToolCalls`, `ValidationResults`, `ApprovalDecisions`, `AuditLogs`. Store structured outputs, tool calls, timings, errors, retries and approval decisions. Do **not** store hidden reasoning, passwords or tokens.

### 5.5 LLM modes
- `LLM_MODE=gemini` (free tier, as in the labs) for the demo.
- `LLM_MODE=mock` for CI and golden-case tests only: deterministic, so evaluation is repeatable. The report must state clearly which mode produced which results.

## 6. Third-party integration
Open-Meteo (free, no API key) for rainfall / flood-risk weather, called only from ASP.NET Core with timeout, retry limit, cache, and a safe fallback (`weatherRisk = Unknown`, flagged for the coordinator). Sends only coordinates, never personal data.

## 7. API inventory (minimum four meaningful endpoints per component)
Shared: `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`, admin user endpoints.
Workflow: `POST /api/workflows`, `GET /api/workflows` (filter, page), `GET /api/workflows/{id}`, `POST /api/workflows/{id}/approve|reject|revise`, `GET /api/workflows/{id}/summary`.

- **A Requests:** `POST/GET /api/relief-requests`, `GET/PUT /api/relief-requests/{id}`, `POST …/{id}/cancel`, `POST …/{id}/triage`
- **B Inventory:** CRUD `/api/depots`, `/api/supply-items`, `/api/stock`; `POST /api/stock/receive`; `POST /api/reservations`; `GET /api/inventory/low-stock`; `GET /api/inventory/availability`
- **C Volunteers:** CRUD `/api/volunteers`; `PUT …/{id}/availability`; `GET /api/volunteers/match`; `POST /api/assignments`
- **D Distribution:** CRUD `/api/vehicles`, `/api/delivery-runs`; `POST …/{id}/schedule`; `PATCH …/{id}/status`; `GET …/{id}/route-risk`
- **E Shelters:** CRUD `/api/shelters`; `POST …/{id}/check-in`; `POST …/{id}/check-out`; `GET /api/shelters/nearest`; `GET /api/reports/summary`

## 8. Database (ER outline)
`Users`, `ReliefRequests`, `ReliefRequestItems`, `Depots`, `SupplyItems`, `StockLevels`, `Reservations`, `Volunteers`, `VolunteerSkills`, `VolunteerAvailability`, `VolunteerAssignments`, `Vehicles`, `DeliveryRuns`, `Shelters`, `ShelterCheckIns`, `AgentWorkflows`, `WorkflowSteps`, `ToolCalls`, `ValidationResults`, `ApprovalDecisions`, `AuditLogs`.
All tables carry `CreatedAt` and `UpdatedAt`. Constraints: stock `>= 0`, shelter occupancy `<= capacity`, unique `(vehicleId, slotStart)`, foreign keys everywhere, indexes on status, district, and date filters.

## 9. Clients
**React (staff):** protected routes by role, CRUD tables with search, filter, sort, pagination; dashboard; workflow monitor; approve / reject / revise panel.
**Flutter (citizens, volunteers):** register, login, secure token storage, request form, GPS location, camera / image picker, status tracking, volunteer assignments.

## 10. Technology decisions (ADR seeds)
1. React state: Zustand for auth and UI state, with API calls through a service layer.
2. Flutter state: Provider with ChangeNotifier.
3. Agent framework: LangGraph in a Python FastAPI service called by ASP.NET Core.
4. Agent state schema: relational tables in PostgreSQL, structured outputs only.
5. Cloud deployment: API on Render (Docker), PostgreSQL on Neon, React on Netlify or Vercel. All free tier.
6. Weather integration: Open-Meteo through the backend.

The group must be able to explain and defend each of these at the viva.

## 11. Build order
1. Foundation: solution, EF Core, auth, roles, workflow tables, CI.
2. Each component vertical slice: API → DB → tests → React → Flutter.
3. Agent service and end-to-end workflow.
4. Testing, agent evaluation, performance numbers (actually run).
5. Deployment, APK, demo video, consolidated report.
