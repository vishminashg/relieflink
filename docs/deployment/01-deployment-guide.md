# ReliefLink Deployment Guide

**Deployment Lead:** Rasara S.H.K (IT21313998, Component B Lead)  
**System Version:** v0.1.0-beta  
**Target Environment:** Free-tier Cloud Architecture (Neon, Render, Netlify/Vercel)

---

## 1. Cloud Architecture Overview

ReliefLink adopts a decoupled cloud-native topology designed to fit within generous free-tier limits while maintaining security, observability, and horizontal scaling.

```mermaid
flowchart TD
    CitizenMobile["Flutter Mobile App (Citizens / Volunteers)"]
    StaffWeb["React + Vite Console (Netlify / Vercel)"]
    
    subgraph CloudInfra ["Cloud Infrastructure (Free Tier)"]
        subgraph RenderPlatform ["Render Cloud Platform"]
            ApiGateway["ASP.NET Core 8 Web API (Docker)"]
            PythonAgent["LangGraph Agent Service (Docker / FastAPI)"]
        end
        
        NeonDb[("Neon Serverless PostgreSQL (pgvector + SSL)")]
        ExternalApi["Open-Meteo Weather API (External)"]
    end
    
    CitizenMobile -->|"HTTPS REST + JWT"| ApiGateway
    StaffWeb -->|"HTTPS REST + JWT (CORS)"| ApiGateway
    ApiGateway -->|"EF Core Migrations & Queries (SSL)"| NeonDb
    ApiGateway -->|"Internal Service-Key Auth"| PythonAgent
    ApiGateway -->|"Geocoded Coordinates"| ExternalApi
    PythonAgent -->|"Read-only Connection"| NeonDb
```

---

## 2. Infrastructure Services Matrix

| Service | Hosting Provider | Runtime / Tier | Public Endpoint | Key Responsibilities |
| :--- | :--- | :--- | :--- | :--- |
| **Relational Database** | **Neon** | Serverless Postgres (v16) | `ep-*.neon.tech` | ACIDs, constraints, EF Core migrations, seeds |
| **Backend API** | **Render** | Docker (Linux Web Service) | `https://relieflink-api.onrender.com` | Business logic, authentication, inventory, audit logs |
| **Agent Microservice** | **Render** | Docker (Python 3.12 Web Service) | `https://relieflink-agents.onrender.com` | LangGraph planning, supply allocation, triage validation |
| **Web Console** | **Netlify / Vercel**| Static Site (React 18 + Vite) | `https://relieflink.netlify.app` | Coordinator / Admin dashboard, inventory operations |
| **Mobile App** | **Android / iOS** | Flutter Client Application | Native Client | Citizen assistance requests, emergency depot views |

---

## 3. Step 1: Database Provisioning (Neon PostgreSQL)

1. **Create Project:**
   - Log in to [Neon.tech](https://neon.tech) and create a project named `relieflink-prod`.
   - Select the closest AWS/GCP region (e.g., `ap-southeast-1` or `eu-central-1`).
   - Neon provides a pooled connection string and a direct connection string.
2. **Obtain Connection String:**
   ```text
   Host=ep-relieflink-prod-123456.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=relieflink_owner;Password=YOUR_SECURE_PASSWORD;SslMode=Require;
   ```
   > [!IMPORTANT]
   > For EF Core migrations and schema DDL on startup, use the **Direct** connection string (Port `5432`) rather than the connection pooler (Port `6543`), as transaction pooling can interfere with migration locks.

---

## 4. Step 2: Backend API Deployment (Render)

The ASP.NET Core 8 Web API is containerized using `api/Dockerfile`.

1. **New Web Service:**
   - In [Render Dashboard](https://dashboard.render.com), click **New +** -> **Web Service**.
   - Connect the GitHub repository: `https://github.com/vishminashg/relieflink`.
   - Branch: `main` (or active staging branch).
   - Root Directory: `api`.
   - Runtime: **Docker**.
   - Dockerfile Path: `./Dockerfile`.
   - Instance Type: **Free**.

2. **Environment Variables:**
   Configure the following in the Render **Environment** tab:

   | Key | Value | Purpose |
   | :--- | :--- | :--- |
   | `ConnectionStrings__Default` | `Host=ep-...;Port=5432;Database=neondb;Username=...;Password=...;SslMode=Require;` | Neon PostgreSQL connection |
   | `Jwt__Key` | *[Minimum 32 random characters]* | HMAC-SHA256 token signing secret |
   | `Jwt__Issuer` | `ReliefLink` | Valid token issuer |
   | `Jwt__Audience` | `ReliefLink` | Valid token audience |
   | `Database__MigrateOnStartup` | `true` | Automatically applies pending EF Core migrations on boot |
   | `Seed__Enabled` | `true` | Pre-seeds demo users, depots, and supply catalog on first run |
   | `Seed__DemoPassword` | `Demo#Pass123` | Password for seeded demo accounts |
   | `Cors__Origins__0` | `https://relieflink.netlify.app` | Production React Web domain |
   | `Cors__Origins__1` | `http://localhost:5173` | Local development preview origin |
   | `Agent__BaseUrl` | `https://relieflink-agents.onrender.com` | Internal LangGraph agent endpoint |
   | `Agent__ApiKey` | *[Shared Service Key]* | Protects internal microservice communication |

3. **Health Check:**
   - Health Check Path: `/api/health`
   - Render will verify that HTTP 200 is returned (which checks `db.Database.CanConnectAsync()`) before routing traffic.

---

## 5. Step 3: Agent Microservice Deployment (Render)

The Python LangGraph service runs via `agents/Dockerfile`.

1. **New Web Service:**
   - Click **New +** -> **Web Service**.
   - Root Directory: `agents`.
   - Runtime: **Docker**.
   - Instance Type: **Free**.

2. **Environment Variables:**

   | Key | Value | Purpose |
   | :--- | :--- | :--- |
   | `AGENT_SHARED_KEY` | *[Same key as Agent__ApiKey]* | Shared token for authentication |
   | `LLM_MODE` | `gemini` (or `mock` for deterministic runs) | Model execution engine |
   | `GEMINI_API_KEY` | *[Google AI Studio Gemini API Key]* | Required when `LLM_MODE=gemini` |
   | `DATABASE_URL` | `postgresql://user:pass@ep-.../neondb?sslmode=require` | Read-only access for agent tools |
   | `PORT` | `8001` | Default port (Render overrides automatically) |

---

## 6. Step 4: Web Console Deployment (Netlify or Vercel)

### Option A: Netlify Deployment
1. Connect repository in [Netlify](https://app.netlify.com).
2. Configuration:
   - **Base directory:** `web`
   - **Build command:** `npm run build`
   - **Publish directory:** `web/dist`
3. Environment Variables:
   - `VITE_API_URL`: `https://relieflink-api.onrender.com`
4. Routing SPA Redirects:
   - The file `web/public/_redirects` contains `/* /index.html 200` to guarantee that browser reloads on deep routes (`/inventory`, `/workflows`, `/requests`) resolve smoothly.

### Option B: Vercel Deployment
1. Import repository in [Vercel](https://vercel.com).
2. Root Directory: `web`.
3. Framework Preset: **Vite**.
4. Environment Variables:
   - `VITE_API_URL`: `https://relieflink-api.onrender.com`
5. The `web/vercel.json` rewrites rules automatically handle Single Page App routing.

---

## 7. Step 5: Mobile App Build (Flutter)

The Flutter application connects to the deployed API via compile-time or runtime configuration:

```bash
cd mobile

# Android Release APK Build
flutter build apk --release --dart-define=API_URL=https://relieflink-api.onrender.com

# The generated APK is located at:
# mobile/build/app/outputs/flutter-apk/app-release.apk
```

---

## 8. Post-Deployment Verification & Smoke Test Checklist

Once all services are deployed, perform the following verification steps:

- [ ] **API Health & DB Connectivity:**
  ```bash
  curl -i https://relieflink-api.onrender.com/api/health
  # Response: HTTP/1.1 200 OK {"status":"healthy","database":"up"}
  ```
- [ ] **Swagger Documentation:**
  Open `https://relieflink-api.onrender.com/swagger` in a browser. Verify all endpoints for Components A, B, C, D, and E are listed.
- [ ] **Staff Authentication:**
  Authenticate using `coordinator@relieflink.local` / `Demo#Pass123`. Verify token generation.
- [ ] **Component B Inventory Verification:**
  - `GET /api/depots` returns 4 seeded Sri Lankan emergency depots (Kalutara, Colombo, Ratnapura, Gampaha).
  - `GET /api/supply-items` returns the standard disaster catalog.
  - `GET /api/stock` returns initial on-hand and available balances.
  - Test a reservation workflow in the Web Console: navigate to `/inventory`, select **Stock Reservations**, and test creating and fulfilling a reservation.
- [ ] **CORS Verification:**
  Open browser developer tools on the Netlify web app, log in, and ensure requests to the Render API execute without CORS preflight failures.

---

## 9. Rollback & Disaster Recovery Procedures

1. **Database Rollbacks:**
   - In the event of a corrupt data state, Neon provides instant point-in-time branch recovery. Create a branch from prior to the incident and update the `ConnectionStrings__Default` host.
2. **Container Rollbacks:**
   - In Render, click the previous successful deploy and select **Rollback to this deploy**.
3. **Zero Negative Stock Invariant Guard:**
   - All stock transactions are guarded by database-level check constraints (`CK_StockLevels_OnHand_GTE_Reserved`). Any errant build cannot corrupt inventory levels below zero.
