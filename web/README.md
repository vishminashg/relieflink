# ReliefLink web console

React + TypeScript console for **coordinators and administrators**. Citizens and volunteers use the mobile app.

## Run it
Prerequisites: Node 20+, and the API running (see the root README).

```bash
cd web
cp .env.example .env.local      # set VITE_API_URL if the API is not on http://localhost:5039
npm ci
npm run dev                     # http://localhost:5173
```

The API must allow this origin: `Cors__Origins__0=http://localhost:5173` (already the default).

## Scripts
| Command | What it does |
|---|---|
| `npm run dev` | Development server with hot reload |
| `npm test` | Unit and component tests (Vitest + Testing Library) |
| `npm run typecheck` | TypeScript in strict mode |
| `npm run build` | Production build into `dist/` |

## Screens
Overview, Requests (filter, search, page), Request detail (triage reasons, urgency override, cancel, start AI plan), AI plans (list), AI plan detail (plan, safety checks per draft, audit trail, approve / ask for new draft / reject / execute).

Design notes and viva questions: [docs/components/A-web-console.md](../docs/components/A-web-console.md)
