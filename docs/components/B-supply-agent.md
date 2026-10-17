# Component B: Supply Agent and AI Governance

Owner: Student 2 (Rasara S.H.K · GitHub: `IT21313998`) · Branch: `feature/inventory`

---

## 1. What this agent does

In emergency disaster relief operations (e.g. floods in Kalutara and landslides in Ratnapura), relief coordinators must allocate emergency supplies (drinking water, dry food packs, medical kits, tarpaulins) quickly without depleting depots or double-allocating items.

The **Supply Agent** is an autonomous specialist in the internal Python FastAPI LangGraph service (`agents/`). It:
1. Receives the relief mission requirements (target incident district, requested quantities per need category, untrusted citizen notes).
2. Uses allow-listed read-only tools (`list_depots`, `check_stock`, `supply_allocation_summary`) to inspect available depot stocks without touching the production database directly.
3. Proposes an optimal allocation plan that minimizes depot splits and prioritizes geographically closest depots.
4. Detects and flags any supply shortages when total available stock across all depots is insufficient.
5. Evaluates logistical risks (e.g., flooded road access or heavy cargo transport requirements).
6. Returns a structured JSON proposal back to the ASP.NET Core backend, where **deterministic C# domain code** verifies availability and enforces hard invariants before coordinator approval.

```
Relief Coordinator / Workflow Engine
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│  Python Agent Service (LangGraph: Supply Agent)             │
│                                                             │
│   gather (tools) ──► draft (LLM) ──► check (self-check)     │
│                            ▲                 │              │
│                            └── retry gaps ───┘              │
│                                      │                      │
│                                      ▼                      │
│                                  finalize                   │
└──────────────────────────────────────┬──────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────┐
│  ASP.NET Core Deterministic Safety Gate (C#)                │
│   - Check stock existence & valid depot IDs                 │
│   - Enforce Non-Negative Invariant: Available >= Requested   │
│   - Transactional atomic reservation (EF Core)              │
└──────────────────────────────────────┬──────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────┐
│  Human Relief Coordinator Review & Approval Gate            │
│   (Approve / Reject / Revise)                               │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Files and what each one does

| File | Purpose |
|---|---|
| `agents/app/supply_agent.py` | LangGraph state graph for the Supply Agent (`gather → draft → check → finalize`). |
| `agents/app/schemas.py` | Contract schemas (`SupplyMatchingInput`, `SupplyProposal`, `SupplyAllocationLine`, `SupplyShortage`, `SupplyAgentResponse`). |
| `agents/app/tools.py` | Allow-listed read-only tools: `list_depots`, `check_stock`, `supply_allocation_summary`. |
| `agents/app/llm.py` | Deterministic `MockLLM.draft_supply` and `GeminiLLM.draft_supply` implementations. |
| `agents/tests/test_supply_agent.py` | Pytest suite testing graph execution, tool calling, self-check loops, and fault injections. |

---

## 3. Allow-Listed Tools (Strictly Read-Only)

The agent cannot execute database writes, network calls, or arbitrary Python commands. It can only call tools on the explicit allow-list:

1. `list_depots(depots, target_district)`:
   - Evaluates all available candidate depots.
   - Computes district match flags (`sameDistrict`), total available units, and item type counts.
   - Sorts depots by proximity and available capacity.

2. `check_stock(depots, needed_category, needed_qty, target_district)`:
   - Scans depot inventories for items matching the needed category.
   - Sorts options by proximity (`sameDistrict = true` first).
   - Computes `totalAvailable`, `shortage = max(0, needed_qty - totalAvailable)`, and `isFullyCovered`.

3. `supply_allocation_summary(lines, needs)`:
   - Post-draft evaluation tool.
   - Aggregates total units allocated, count of distinct depots utilized, and identifies any remaining unmet needs.

---

## 4. Prompt Engineering & Defenses Against Prompt Injection

Citizens submit free-text `notes` describing their situation (e.g. "We need water immediately, bridge is washed away"). These notes are untrusted input.

The Supply Agent implements defense-in-depth:
1. **Tool Isolation:** Tools do not receive or execute citizen notes. Tools operate solely on structured facts (`needs`, `depots`).
2. **Text Sanitization:** The `sanitize_text` function strips all HTML brackets (`<`, `>`) and regular-expression matches for URLs (`http://`, `https://`), replacing them with `[link removed]` to prevent phishing or stored XSS.
3. **No Direct Execution:** The agent never reserves or moves stock. It only proposes an allocation. The C# backend enforces mathematical validation, and a human coordinator must explicitly click "Approve".

---

## 5. Self-Check Loop in LangGraph

Before returning an allocation proposal, the agent's internal `check` node runs deterministic validations:
- **Non-positive quantity check:** Confirms all allocated quantities are $> 0$.
- **Over-allocation check:** Verifies that no category was allocated more than requested.
- **Retry loop:** If problems are found and fewer than 2 drafts have been attempted, the graph loops back to `draft` with the specific error reasons injected. Otherwise, it proceeds to `finalize`.
