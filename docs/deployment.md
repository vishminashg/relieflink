# Deployment (free tiers)

| Part | Host | Source |
|---|---|---|
| Database | Neon (PostgreSQL) | neon.tech |
| API | Render, Docker web service | `api/Dockerfile`, root directory `api` |
| Planner agent | Render, Docker web service | `agents/Dockerfile`, root directory `agents` |
| Web console | Netlify | `web/netlify.toml` |
| Mobile app | Release APK installed on a phone | `docs/apk-install.md` |

Free Render services sleep after about 15 minutes without traffic. The first request after that takes 30 to 60 seconds. The mobile app waits up to 60 seconds for a response for this reason.

## 1. Database (Neon)
Create a project and copy the connection string (`postgresql://user:password@host/db?sslmode=require`). Paste it as-is into the API's `ConnectionStrings__Default`. The API converts that URI form itself (`Data/ConnectionStringNormalizer.cs`) and uses SSL.

## 2. Planner agent (Render)
New Web Service, repository connected, runtime Docker, root directory `agents`.

| Variable | Value |
|---|---|
| `LLM_MODE` | `mock` (works offline) or `gemini` |
| `GEMINI_API_KEY` | only when `LLM_MODE=gemini` |
| `GEMINI_MODEL` | `gemini-3.5-flash` |
| `AGENT_SERVICE_KEY` | a long random string, reused below |

Check `https://<agent>.onrender.com/health`.

## 3. API (Render)
New Web Service, runtime Docker, root directory `api`.

| Variable | Value |
|---|---|
| `ConnectionStrings__Default` | the Neon URI |
| `Jwt__Key` | a random string of 32+ characters |
| `Database__MigrateOnStartup` | `true` |
| `Seed__Enabled` | `true` |
| `Seed__DemoPassword` | a password you choose (not stored in the repository) |
| `Agent__BaseUrl` | `https://<agent>.onrender.com` |
| `Agent__ServiceKey` | the same value as `AGENT_SERVICE_KEY` |
| `Cors__Origins__0` | the Netlify web address, once known |

Check `https://<api>.onrender.com/api/health`. It should answer `healthy` with `database: up`.

## 4. Web console (Netlify)
Import the repository. `web/netlify.toml` sets the build. Add the variable `VITE_API_URL` = `https://<api>.onrender.com` before building. Then put the Netlify address into the API's `Cors__Origins__0` and redeploy the API.

## 5. Mobile
See `docs/apk-install.md`.
