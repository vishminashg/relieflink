# ReliefLink

Disaster relief coordination platform for Sri Lanka. SE3090 Assignment 1 (Integrated Full-Stack and Agentic AI Application).

Status: shared foundation complete (auth, roles, workflow state, tests, CI). Component work in progress. The final README (live URLs, contributions, AI usage declaration) is completed at the end.

## Repository layout
| Folder | Contents |
|---|---|
| `api/` | ASP.NET Core 8 Web API, EF Core + PostgreSQL, xUnit tests |
| `web/` | React web console for coordinators and admins |
| `mobile/` | Flutter app for citizens and volunteers |
| `agents/` | Python FastAPI + LangGraph agent service (internal only) |
| `docs/` | Scope and design, ADRs, ER diagram, reports |
| `.github/workflows/` | CI pipelines |

Design baseline: [docs/00-scope-and-design.md](docs/00-scope-and-design.md)

## Run the API locally
Prerequisites: .NET 8 SDK, PostgreSQL 14+.

```bash
# 1. Create a database
createdb relieflink

# 2. Configure (see .env.example). Minimum:
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=relieflink;Username=postgres;Password=YOUR_PASSWORD"
export Jwt__Key="$(openssl rand -base64 48)"
export Database__MigrateOnStartup=true      # applies migrations on start
export Seed__Enabled=true                   # creates one demo user per role
export Seed__DemoPassword="ChooseOne123"

# 3. Run
cd api/ReliefLink.Api
dotnet run
# Swagger UI: http://localhost:5039/swagger   Health: /api/health
```

Demo accounts (created only when `Seed__Enabled=true`; password is whatever you set in `Seed__DemoPassword`):

| Role | Email |
|---|---|
| Admin | admin@relieflink.lk |
| Coordinator | coordinator@relieflink.lk |
| Volunteer | volunteer@relieflink.lk |
| Citizen | citizen@relieflink.lk |

## Run the AI agent service (needed for the relief workflow)
```bash
cd agents && pip install -r requirements-dev.txt
export AGENT_SERVICE_KEY="$(openssl rand -hex 24)" LLM_MODE=mock
uvicorn app.main:app_factory --factory --port 8001
# then start the API with  Agent__BaseUrl=http://localhost:8001  Agent__ServiceKey=<same key>
```
Details: [agents/README.md](agents/README.md). With the API and the agent service both running, `python scripts/e2e_smoke.py --password <Seed__DemoPassword>` walks one request through the whole workflow.

## Run the web console
```bash
cd web && cp .env.example .env.local && npm ci && npm run dev    # http://localhost:5173
```
Sign in with the coordinator or admin demo account. Details: [web/README.md](web/README.md).

## Run the mobile app
Flutter 3.47 and an Android emulator or phone. The installable APK is built by CI: see [mobile/README.md](mobile/README.md).

## Run the tests
The integration tests use a real PostgreSQL server. Each run creates a uniquely named database, applies the real EF Core migrations, and drops it afterwards.

```bash
export ConnectionStrings__TestDb="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD"
dotnet test api/ReliefLink.sln
```

Agent service tests: `cd agents && pytest`.

## Adding a migration (each student does this for their own component)
```bash
dotnet tool install --global dotnet-ef --version 8.0.11
cd api/ReliefLink.Api
dotnet ef migrations add <YourComponentName> -o Data/Migrations
```

## Foundation API (already implemented)
| Method | Path | Access |
|---|---|---|
| POST | `/api/auth/register` | Public (Citizen or Volunteer only) |
| POST | `/api/auth/login` | Public |
| GET | `/api/auth/me` | Any signed-in user |
| GET / POST | `/api/users` | Admin |
| PATCH | `/api/users/{id}/active` | Admin |
| GET | `/api/health` | Public |

## Conventions for adding a component
- Entities inherit `BaseEntity` (Id, CreatedAt, UpdatedAt are stamped automatically).
- Register the entity and its constraints in `Data/AppDbContext.cs`.
- Protect controllers with `[Authorize(Roles = "...")]`. Never trust a role or user id sent in a request body; read it from the token (`User.GetUserId()`).
- Write audit rows with `IAuditService.Record(...)` in the same `SaveChanges` as the business change.
- Workflow status changes go only through `WorkflowStateMachine.Transition(...)`.
- Add tests next to the existing ones. Integration tests use `[Collection(ApiCollection.Name)]` and `ApiFactory`.
