# Component C: Volunteer Management and Matching Agent

- Student: Nagahawatta H.D.
- GitHub: `Hasindu-Dilshan`
- Component: C. Volunteer Management
- Agent: Volunteer Matching Agent
- Branch: `feature/volunteers`
- I have read the scope and design document: yes

---


## 1. What this component does
During natural disasters in Sri Lanka (such as floods and landslides in Kalutara, Gampaha, Ratnapura, and Kegalle), volunteer response is crucial but frequently chaotic. Volunteers with critical skills (such as boat operation, first aid, or medical assistance) are either under-utilized, dispatched to locations where they cannot help, or double-booked across competing relief tasks.

**Component C: Volunteer Management** provides the end-to-end management of volunteers and their deployment:
1. **Volunteer Profiles & Skills:** Volunteers register their verified skills (`Medical`, `FirstAid`, `SearchAndRescue`, `BoatOperation`, `HeavyVehicleDriving`, `FoodDistribution`, `ChildCare`, `ElderlyCare`) and operational district.
2. **Availability Windows:** Volunteers define when they can be deployed (both recurring weekly schedules and specific date-time ranges).
3. **Skill & Availability Matching Engine (Core Business Logic):** A deterministic, transparent matching algorithm matches volunteers to relief requests based on required skills, district proximity, and active availability windows.
4. **Strict No Double-Booking Guarantee:** The scheduling engine enforces conflict-free assignment windows. A volunteer can never be assigned to overlapping relief tasks or delivery runs, backed by mathematical interval checks and database-level transactional locks.
5. **Volunteer Assignment Lifecycle:** Coordinators dispatch volunteers, volunteers track their active assignments in the mobile app, and status updates (`Assigned → InProgress → Completed` or `Cancelled`) are tracked with audit logging.
6. **Volunteer Matching AI Agent:** A specialized, read-only LangGraph agent that assists coordinators by evaluating complex relief needs, querying available candidate pools via allow-listed tools, and proposing optimal volunteer rosters.

---

## 2. Files and what each one does

| File | Purpose |
|---|---|
| `Domain/Volunteer.cs` | Core entities (`Volunteer`, `VolunteerSkill`, `VolunteerAvailability`, `VolunteerAssignment`) and enums (`VolunteerSkillType`, `AvailabilityType`, `AssignmentStatus`, `SkillProficiency`). |
| `Domain/VolunteerMatcher.cs` | Pure domain function for skill matching, proximity scoring, and interval overlap detection (**No double-booking**). |
| `Domain/AssignmentStatusRules.cs` | Enforces the valid state machine for volunteer assignments and role-based action permissions. |
| `Services/VolunteerValidator.cs` | Centralized validation for profiles, skill inputs, availability intervals, and assignment requests. |
| `Controllers/VolunteersController.cs` | Endpoints for volunteer profile CRUD, availability updates, skill updates, and match queries. |
| `Controllers/AssignmentsController.cs` | Endpoints for creating assignments, listing volunteer assignments, and updating status with audit logging. |
| `Controllers/Dtos/VolunteerDtos.cs` | Request and response contract DTOs for volunteers, availabilities, matching results, and assignments. |
| `Data/Configurations/VolunteerConfiguration.cs` | Entity type configurations: table mappings, PostgreSQL check constraints, foreign keys, and indexes. |
| `Data/AppDbContext.Volunteers.cs` | Partial `AppDbContext` registering Component C's `DbSet` collections without modifying shared files. |
| `Data/Migrations/*AddVolunteerManagement*` | EF Core migration creating tables, constraints, and indexes. |
| `agents/app/volunteer_agent.py` | Python LangGraph agent for volunteer matching with structured prompt and output schema. |
| `agents/app/tools.py` (extension) | Allow-listed read-only tool `find_volunteers` querying available, unassigned volunteers. |

---

## 3. API Endpoints

### A. Volunteers & Availability (`/api/volunteers`)

| Method | Path | Allowed Roles | Description |
|---|---|---|---|
| `POST` | `/api/volunteers` | Volunteer, Staff | Register volunteer profile (automatically linked to authenticated user). |
| `GET` | `/api/volunteers` | Coordinator, Admin | List volunteers with filters (skill, district, status, availability, search), sorting, and pagination. |
| `GET` | `/api/volunteers/{id}` | Owner (Volunteer), Staff | Get detailed volunteer profile including skills and upcoming availability. |
| `PUT` | `/api/volunteers/{id}` | Owner (Volunteer), Staff | Update profile details (phone, district, emergency contact, active status). |
| `PUT` | `/api/volunteers/{id}/skills` | Owner (Volunteer), Staff | Replace/update certified skill sets and proficiency levels. |
| `PUT` | `/api/volunteers/{id}/availability` | Owner (Volunteer), Staff | Set or update availability time slots (recurring weekly or specific dates). |
| `GET` | `/api/volunteers/match` | Coordinator, Admin | **Core Business Operation:** Find and rank available volunteers by required skills, district, and time window without scheduling conflicts. |
| `GET` | `/api/volunteers/summary` | Coordinator, Admin | Dashboard metrics: total active volunteers, counts by district, top available skills, and current deployment load. |

### B. Volunteer Assignments (`/api/assignments`)

| Method | Path | Allowed Roles | Description |
|---|---|---|---|
| `POST` | `/api/assignments` | Coordinator, Admin | Create assignment for a relief request or delivery run. Atomically verifies no double-booking! |
| `GET` | `/api/assignments/my` | Volunteer | List assignments assigned to the calling volunteer (past, active, and upcoming). |
| `GET` | `/api/assignments/{id}` | Assigned Volunteer, Staff | Get single assignment details and task description. |
| `PATCH` | `/api/assignments/{id}/status` | Assigned Volunteer, Staff | Transition status (`Assigned → InProgress → Completed` or `Cancelled`) with optional notes. |

---

## 4. Business Rules & Algorithms

### 4.1 Skill & Availability Matching Engine (`VolunteerMatcher`)
When a relief request or delivery run requires volunteer support, the matching engine scores candidate volunteers from **0 to 100** based on three key pillars:

$$\text{Total Score} = \text{Skill Match (45 pts)} + \text{Proximity (35 pts)} + \text{Reliability \& Experience (20 pts)}$$

| Component | Points | Calculation Rules |
|---|---|---|
| **Mandatory Skills** | Filter Gate | Volunteer **must** possess all mandatory required skills (e.g. `BoatOperation` for flooded flood rescue). Non-qualifying volunteers are excluded. |
| **Skill Breadth / Bonus** | Up to 45 pts | $30 \text{ pts}$ for possessing all required skills + up to $15 \text{ pts}$ for complementary skills (e.g., `FirstAid` alongside `BoatOperation`). |
| **District Proximity** | Up to 35 pts | **Same district** = $35 \text{ pts}$<br>**Adjacent/Neighboring district** = $20 \text{ pts}$<br>**Other district** = $5 \text{ pts}$ |
| **Experience & Reliability** | Up to 20 pts | $2 \text{ pts}$ per completed past assignment (capped at $10 \text{ pts}$) + $10 \text{ pts}$ for verified/certified skills. |

---

### 4.2 Conflict-Free Scheduling: The Strict No Double-Booking Invariant

A volunteer **cannot be double-booked** across overlapping operational time windows.

#### Mathematical Overlap Condition:
Two time intervals $[S_1, E_1]$ and $[S_2, E_2]$ overlap if and only if:
$$\max(S_1, S_2) < \min(E_1, E_2)$$

#### Operational Buffer:
To allow transit and rest between disaster relief operations, the system enforces a **30-minute buffer window**:
$$S_2 < E_1 + 30\text{ minutes} \quad \text{and} \quad S_1 < E_2 + 30\text{ minutes}$$

#### Conflict Checking Rules:
1. **Existing Active Assignments:** Any assignment in `Assigned` or `InProgress` status is considered active.
2. **Cancelled / Completed Assignments:** These release the volunteer's time slot and do not produce conflicts.
3. **Availability Envelope Check:** The assignment's time window must fall within one of the volunteer's registered availability slots (`VolunteerAvailability`).
4. **Concurrency Safety:** Assignment creation uses database transactions and optimistic/pessimistic locking to prevent race conditions when two coordinators attempt to assign the same volunteer simultaneously.

---

### 4.3 Assignment Lifecycle State Machine

```
                  ┌───────────────┐
                  │   Assigned    │
                  └───────┬───────┘
                          │
             ┌────────────┴────────────┐
             ▼                         ▼
      ┌──────────────┐          ┌──────────────┐
      │  InProgress  │          │  Cancelled   │ (Coordinator or Emergency Drop)
      └──────┬───────┘          └──────────────┘
             │                         ▲
             ├─────────────────────────┘
             ▼
      ┌──────────────┐
      │  Completed   │ (Mission fulfilled)
      └──────────────┘
```

* **Legal transitions:**
  * `Assigned → InProgress` (Volunteer reports on duty)
  * `InProgress → Completed` (Volunteer completes mission)
  * `Assigned → Cancelled` (Mission cancelled or reassigned)
  * `InProgress → Cancelled` (Aborted due to safety/weather risks)
* **Terminal states:** `Completed`, `Cancelled`.

---

## 5. Security and Privacy Decisions

1. **Privacy-Preserving Access Control:**
   * Volunteers can only view and update **their own profile, availability, and assignments**.
   * Personal details (phone number, home address, emergency contact) are never exposed to citizens.
   * If a volunteer attempts to view another volunteer's record, the API returns **404 Not Found** (not 403), preventing user enumeration.
2. **Token-Derived Identity:**
   * The authenticated user's ID is extracted exclusively from the JWT claims (`User.GetUserId()`). Clients cannot supply a arbitrary `userId` or `volunteerId` in mutation requests.
3. **Audit Trail:**
   * Every assignment creation, cancellation, status transition, and availability change writes a structured row to `AuditLogs` within the same database transaction.
4. **Defense in Depth (Database Constraints):**
   * Check constraints guarantee:
     * Assignment `EndTime > StartTime`.
     * Availability `EndTime > StartTime`.
     * Phone number formats and valid district names from the official 25 Sri Lankan districts list (`SriLanka.Districts`).

---

## 6. Database Design (ER Outline)

### Tables & Relationships

```
Users (Foundation)
  │
  ├── 1 : 1 ──► Volunteers
                  │
                  ├── 1 : N ──► VolunteerSkills (SkillType, CertificationRef, Verified)
                  ├── 1 : N ──► VolunteerAvailabilities (DayOfWeek, Date, StartTime, EndTime)
                  └── 1 : N ──► VolunteerAssignments (ReliefRequestId, StartTime, EndTime, Status)
```

1. **`Volunteers`:**
   * `Id` (PK, Guid)
   * `UserId` (FK → `Users.Id`, Unique, DeleteBehavior.Restrict)
   * `District` (varchar(60), indexed)
   * `EmergencyContactName` (varchar(120))
   * `EmergencyContactPhone` (varchar(30))
   * `IsActive` (bool, default true, indexed)
   * `MaxHoursPerWeek` (int, default 40)
2. **`VolunteerSkills`:**
   * `Id` (PK, Guid)
   * `VolunteerId` (FK → `Volunteers.Id`, DeleteBehavior.Cascade)
   * `Skill` (varchar(40), stored as string enum)
   * `Proficiency` (varchar(20), `Beginner`, `Intermediate`, `Advanced`, `Expert`)
   * `CertificateReference` (varchar(100), optional)
   * `IsVerified` (bool, default false)
   * *Unique constraint:* `(VolunteerId, Skill)`
3. **`VolunteerAvailabilities`:**
   * `Id` (PK, Guid)
   * `VolunteerId` (FK → `Volunteers.Id`, DeleteBehavior.Cascade)
   * `Type` (varchar(20), `RecurringWeekly` or `SpecificDate`)
   * `DayOfWeek` (int, nullable, 0-6)
   * `SpecificDate` (date, nullable)
   * `StartTime` (time/DateTime)
   * `EndTime` (time/DateTime)
   * *Check constraint:* `EndTime > StartTime`
4. **`VolunteerAssignments`:**
   * `Id` (PK, Guid)
   * `VolunteerId` (FK → `Volunteers.Id`, DeleteBehavior.Restrict)
   * `ReliefRequestId` (FK → `ReliefRequests.Id`, nullable, DeleteBehavior.Restrict)
   * `StartTime` (timestamp with time zone)
   * `EndTime` (timestamp with time zone)
   * `Status` (varchar(30), `Assigned`, `InProgress`, `Completed`, `Cancelled`)
   * `Notes` (varchar(1000), optional)
   * `CreatedByUserId` (FK → `Users.Id`)
   * *Indexes:* `VolunteerId`, `Status`, `(StartTime, EndTime)`
   * *Check constraint:* `EndTime > StartTime`

---

## 7. Agentic AI: Volunteer Matching Agent

### 7.1 Architecture & Role in Workflow
The **Volunteer Matching Agent** runs inside the Python FastAPI + LangGraph agent service (`agents/`). It participates in the multi-agent relief planning workflow orchestrated by ASP.NET Core:

```
ASP.NET Core WorkflowEngine
          │
          ▼  POST /agents/volunteers (Internal HTTP, X-Service-Key)
Python Agent Service (LangGraph)
    ├── gather: calls allow-listed tool find_volunteers(...)
    ├── score: ranks candidates based on skills, district, and availability
    ├── draft: generates structured VolunteerProposal
    └── validate: self-checks constraints
          │
          ▼
ASP.NET Core Deterministic Re-Validation (C#)
    ├── verifies volunteer existence & active status
    ├── validates no double-booking overlap
    └── ensures required skills are covered
```

### 7.2 Input and Output Contracts

#### Input (`VolunteerMatchingInput`):
* `requestId` (Guid)
* `district` (string)
* `requiredSkills` (list of strings, e.g. `["BoatOperation", "FirstAid"]`)
* `startTime` (ISO 8601 UTC)
* `endTime` (ISO 8601 UTC)
* `volunteersNeeded` (int, 1-10)

#### Output (`VolunteerMatchingResponse`):
* `proposal`:
  * `volunteerIds`: list of selected volunteer IDs
  * `summary`: plain-language reasoning for the selection
  * `skillsCovered`: list of skills satisfied by the group
* `toolCalls`: list of recorded tool calls for coordinator transparency

### 7.3 Read-Only Tool: `find_volunteers`
* **Allow-listed:** Only read-only queries against volunteer skills, districts, and availability intervals.
* **No writes:** The agent cannot create assignments or modify schedules; writes occur only in ASP.NET Core after coordinator approval.

---

## 8. Test Strategy

1. **Unit Tests (`ReliefLink.Tests/Unit/`):**
   * `VolunteerMatcherTests`:
     * Full skill match vs partial skill match.
     * Proximity scoring (same district vs neighboring district).
     * No double-booking conflict detection: exact match, partial overlap, surrounding overlap, buffer window overlap.
     * Edge cases: zero available volunteers, insufficient skills, boundary times.
   * `AssignmentStatusRulesTests`:
     * Legal transitions (`Assigned → InProgress → Completed`).
     * Illegal transitions (e.g. `Completed → InProgress`).
     * Cancellation rules.
   * `VolunteerValidatorTests`:
     * Negative intervals (`EndTime <= StartTime`).
     * Invalid skill types or phone numbers.
     * Maximum hours per week boundaries.
2. **Integration Tests (`ReliefLink.Tests/Integration/VolunteersTests.cs`):**
   * Real PostgreSQL database with EF Core migrations.
   * Role authorization checks (Citizen cannot access volunteer lists, Volunteer cannot view others).
   * Double-booking rejection: second assignment in the same slot returns `409 Conflict`.
   * Concurrent assignment safety test.
   * Audit log verification for assignment creation and status changes.
3. **Agent Service Tests (`agents/tests/test_volunteer_agent.py`):**
   * Unit tests for tool execution and LangGraph graph transitions.
   * Deterministic mock mode verification.

---

## 9. Likely Viva Questions & Answers

1. **Why is the double-booking check done in C# code rather than by the AI agent?**
   * *Answer:* AI outputs are probabilistic and non-deterministic. A double-booked volunteer directly compromises disaster relief on the ground. Enforcing conflict-free scheduling in deterministic C# code (and at the database level) guarantees zero double-booking regardless of model output or hallucination.

2. **How do you mathematically determine if two assignments overlap?**
   * *Answer:* Two intervals $[S_1, E_1]$ and $[S_2, E_2]$ overlap if $\max(S_1, S_2) < \min(E_1, E_2)$. We also add a 30-minute operational transit buffer between assignments.

3. **How do you prevent race conditions if two coordinators assign the same volunteer simultaneously?**
   * *Answer:* We wrap assignment creation in a database transaction with a check on active assignment intervals. If an overlapping active assignment exists, EF Core throws a conflict exception, returning HTTP 409.

4. **Why are volunteer phone numbers and emergency contacts hidden from citizens?**
   * *Answer:* Data privacy and protection. Citizens only need to know that relief is assigned or on the way. Exposing volunteers' personal contact info opens risks of harassment or off-platform demands during stressful disasters.

5. **Why return HTTP 404 instead of 403 when a volunteer requests another volunteer's record?**
   * *Answer:* Returning 403 confirms the existence of a volunteer ID (resource enumeration). Returning 404 treats unauthorized resources as non-existent, defending against information leakage.

6. **Why use separate partial classes (`AppDbContext.Volunteers.cs`) and configuration files?**
   * *Answer:* In a 5-student team working in parallel, editing a single `AppDbContext.cs` creates frequent git merge conflicts. Partial classes and `IEntityTypeConfiguration<T>` allow each student to own their component's schema independently.

7. **How does the Volunteer Matching Agent handle situations where no volunteer has all required skills?**
   * *Answer:* The agent identifies the skill gap, selects the closest matching team (e.g. pairing one volunteer with `BoatOperation` and another with `FirstAid`), and explicitly flags the unmet skills in the proposal rationale for coordinator review.

8. **What happens to assignments if a relief request is cancelled?**
   * *Answer:* When a request is cancelled, all associated volunteer assignments transition to `Cancelled`, releasing the volunteers' schedules for other relief efforts.

9. **Why is `find_volunteers` a read-only tool?**
   * *Answer:* Architecture principle: agents propose, humans approve, backend commits. The agent should never write directly to PostgreSQL or reserve resources autonomously.

10. **What would you improve with more time?**
    * *Answer:* Real-time GPS location tracking for active deployments, SMS notification dispatch via Sri Lankan telecom gateways (e.g. Dialog Ideamart), and automated route transit calculation using OpenStreetMap.

---

## 10. Live Coding Changes to Practice for Viva

1. **Change the transit buffer from 30 minutes to 45 minutes** in `VolunteerMatcher.TransitBufferMinutes` and verify the unit tests that catch the boundary.
2. **Add a new volunteer skill `DroneOperation`** (add enum member, update matching weights, verify migration and tests).
3. **Change the maximum weekly hours limit** from 40 to 48 in `VolunteerValidator`.
4. **Add a filter to the match endpoint** to optionally exclude unverified volunteers.
5. **Change the default volunteer list page size** from 20 to 15.

---

## 11. Known Limitations

* Geocoding is district-level rather than real-time GPS coordinates for route calculations.
* Availability slots are currently defined in UTC without automated local daylight savings conversion (Sri Lanka operates on fixed UTC+5:30).
* Notification delivery to volunteers is simulated via API status polling rather than push notifications (FCM).
