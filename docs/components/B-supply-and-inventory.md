# Component B: Supply and Inventory Management

Owner: Student 2 (Rasara S.H.K · GitHub: `IT21313998`) · Branch: `feature/inventory`

---

## 1. What this component does

During natural disasters in Sri Lanka (such as floods and landslides across Kalutara, Gampaha, Ratnapura, and Kegalle), relief coordination breaks down when inventory is tracked on paper or messaging groups:
- Relief depots have stock that field coordinators do not know about.
- Donated supplies sit undistributed in one district while families 20 km away have no drinking water.
- Stock is double-allocated to multiple missions, resulting in dispatch trucks arriving to empty warehouses.
- Physical stock counts go negative due to uncoordinated dispatches.

**Component B: Supply and Inventory Management** provides the end-to-end management of emergency supplies, warehouses, stock movements, and transactional reservations:

1. **Depots & Logistics Hubs (`/api/depots`):** Central logistics hubs (Colombo) and district-level emergency relief depots (Kalutara, Gampaha, Ratnapura) with GPS coordinates, storage capacities, and manager contacts.
2. **Supply Catalog (`/api/supply-items`):** Standard relief items across all disaster need categories (`Food`, `Water`, `Medical`, `Shelter`, `Sanitation`, `Clothing`), including standard family allocations and reorder thresholds.
3. **Real-time Stock Tracking (`/api/stock`):** Real-time tracking of physical `QuantityOnHand`, committed `QuantityReserved`, and immediately deployable `QuantityAvailable = QuantityOnHand - QuantityReserved`.
4. **Strict Zero Negative Stock Guarantee (Core Business Operation):** All stock allocations and dispatches are transactional. Database-level check constraints (`QuantityOnHand >= 0`, `QuantityReserved >= 0`, `QuantityOnHand >= QuantityReserved`) and domain logic guarantee that inventory can **never drop below zero**.
5. **Transactional Stock Reservations (`/api/reservations`):** When an AI relief plan or coordinator allocates items for a mission, stock is reserved atomically with an expiration window. If a mission is cancelled, stock is released back into the available pool. On dispatch, fulfillment permanently deducts on-hand stock.
6. **Low-Stock Safety Monitoring (`/api/inventory/low-stock`):** Real-time alerts for depots where available units fall to or below the safety reorder threshold, prompting replenishment from central hubs.
7. **Supply AI Agent (`POST /agents/supply`):** A dedicated Python LangGraph agent that inspects candidate depot stock via allow-listed read-only tools, optimizes multi-depot allocations by proximity, and flags shortages.

---

## 2. Files and what each one does

| File | Purpose |
|---|---|
| `Domain/Inventory.cs` | Domain entities: `Depot`, `SupplyItem`, `StockLevel`, `StockReservation`, `StockTransaction`, and enums (`ReservationStatus`, `StockTransactionType`). |
| `Domain/InventoryManager.cs` | Pure domain logic: transactional reservation allocation, fulfillment, release, receiving, count adjustments, and district proximity scoring. |
| `Services/InventoryValidator.cs` | Centralized validation for depots, supply items, stock receipts, and reservations. |
| `Controllers/DepotsController.cs` | REST endpoints for listing depots (with search/district filter), depot details, stock summaries, creation, updates, and soft-deactivation. |
| `Controllers/SupplyItemsController.cs` | Catalog endpoints: item CRUD, category filters, reorder thresholds, and family allocations. |
| `Controllers/InventoryController.cs` | Stock endpoints: stock queries, availability by district, low-stock alerts, stock receipt, adjustments, and reservation lifecycle (create, fulfill, cancel). |
| `Controllers/Dtos/InventoryDtos.cs` | Request and response contracts for all inventory operations. |
| `Data/Configurations/InventoryConfiguration.cs` | EF Core entity configurations with PostgreSQL check constraints and unique indexes. |
| `Data/AppDbContext.Inventory.cs` | Partial `AppDbContext` registering Component B's `DbSet` collections. |
| `Data/Migrations/*AddSupplyAndInventory*` | EF Core migration creating database tables, indexes, and constraints. |
| `agents/app/supply_agent.py` | Python LangGraph agent for supply allocation (`gather → draft → check → finalize`). |
| `agents/app/tools.py` | Allow-listed read-only tools: `list_depots`, `check_stock`, `supply_allocation_summary`. |
| `web/src/pages/Inventory.tsx` | React management console with KPI stats, low-stock warnings, and tabs for Stock Levels, Reservations, Catalog, and Depots. |
| `mobile/lib/screens/inventory_screen.dart` | Flutter mobile screen allowing volunteers and citizens to inspect available supplies in their district. |

---

## 3. API Endpoints

### A. Depots (`/api/depots`)

| Method | Path | Allowed Roles | Description |
|---|---|---|---|
| `GET` | `/api/depots` | Authenticated | List depots with optional `district`, `search`, and `activeOnly` filters. |
| `GET` | `/api/depots/{id}` | Authenticated | Get single depot details. |
| `GET` | `/api/depots/{id}/summary` | Authenticated | Get depot stock summary (total item types, on-hand, reserved, available). |
| `POST` | `/api/depots` | Coordinator, Admin | Register a new depot warehouse. Validates district and coordinates. |
| `PUT` | `/api/depots/{id}` | Coordinator, Admin | Update depot name, address, contact details, and capacity. |
| `DELETE` | `/api/depots/{id}` | Admin | Soft-deactivate depot (prevented if active reservations exist). |

### B. Supply Items (`/api/supply-items`)

| Method | Path | Allowed Roles | Description |
|---|---|---|---|
| `GET` | `/api/supply-items` | Authenticated | List items with `category`, `search`, and `activeOnly` filters. |
| `GET` | `/api/supply-items/{id}` | Authenticated | Get supply item specification by ID. |
| `POST` | `/api/supply-items` | Coordinator, Admin | Add new item to relief catalog with reorder threshold and standard allocation. |
| `PUT` | `/api/supply-items/{id}` | Coordinator, Admin | Update catalog item details and thresholds. |

### C. Stock & Reservations (`/api/stock` & `/api/reservations`)

| Method | Path | Allowed Roles | Description |
|---|---|---|---|
| `GET` | `/api/stock` | Authenticated | Query stock across depots with `depotId`, `category`, and `isLowStock` filters. |
| `GET` | `/api/inventory/availability` | Authenticated | Check available stock for a category/item across depots, ranked by proximity. |
| `GET` | `/api/inventory/low-stock` | Authenticated | Dashboard alerts for all items at or below safety reorder thresholds. |
| `GET` | `/api/inventory/summary` | Authenticated | Dashboard metrics: total on-hand, reserved, available, depots, low-stock count. |
| `POST` | `/api/stock/receive` | Coordinator, Admin | Receive incoming physical shipment into a depot with transaction audit trail. |
| `POST` | `/api/stock/adjust` | Coordinator, Admin | Audit count adjustment (strictly prevented from dropping below active reservations). |
| `POST` | `/api/reservations` | Coordinator, Admin | **Core Business Operation:** Atomically reserve stock for relief mission. Guarantees no negative stock! |
| `GET` | `/api/reservations` | Authenticated | List reservations with optional `depotId`, `reliefRequestId`, or `status` filters. |
| `GET` | `/api/reservations/{id}` | Authenticated | Get single reservation details. |
| `POST` | `/api/reservations/{id}/fulfill` | Coordinator, Admin | Fulfill and dispatch reserved stock, permanently decrementing physical on-hand stock. |
| `POST` | `/api/reservations/{id}/cancel` | Coordinator, Admin | Cancel reservation and restore reserved units back to available pool. |

---

## 4. Concurrency & Non-Negative Stock Invariants

To guarantee that relief operations never attempt to dispatch non-existent stock:

1. **Mathematical Invariant:**
   $$\text{QuantityAvailable} = \text{QuantityOnHand} - \text{QuantityReserved} \ge 0$$

2. **Database Check Constraints:**
   ```sql
   ALTER TABLE "StockLevels" ADD CONSTRAINT "CK_StockLevels_OnHand_NonNegative" CHECK ("QuantityOnHand" >= 0);
   ALTER TABLE "StockLevels" ADD CONSTRAINT "CK_StockLevels_Reserved_NonNegative" CHECK ("QuantityReserved" >= 0);
   ALTER TABLE "StockLevels" ADD CONSTRAINT "CK_StockLevels_OnHand_GTE_Reserved" CHECK ("QuantityOnHand" >= "QuantityReserved");
   ALTER TABLE "StockReservations" ADD CONSTRAINT "CK_StockReservations_Quantity_Positive" CHECK ("Quantity" > 0);
   ```

3. **Atomic State Transitions:**
   - **Reserve:** `QuantityReserved += requestedQty` (requires `QuantityAvailable >= requestedQty`).
   - **Cancel:** `QuantityReserved -= reservationQty`, status `Cancelled`.
   - **Fulfill:** `QuantityOnHand -= reservationQty`, `QuantityReserved -= reservationQty`, status `Fulfilled`.
   - **Receive:** `QuantityOnHand += receivedQty`, updates `LastRestockedAt`.
