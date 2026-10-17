import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { Depot, InventorySummary, LowStockAlert, StockLevel, StockReservation, SupplyItem } from "../api/types";

export function Inventory() {
  const [tab, setTab] = useState<"stock" | "items" | "depots" | "reservations">("stock");
  const [summary, setSummary] = useState<InventorySummary | null>(null);
  const [alerts, setAlerts] = useState<LowStockAlert[]>([]);
  const [stock, setStock] = useState<StockLevel[]>([]);
  const [items, setItems] = useState<SupplyItem[]>([]);
  const [depots, setDepots] = useState<Depot[]>([]);
  const [reservations, setReservations] = useState<StockReservation[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  // Filters
  const [search, setSearch] = useState("");
  const [selectedDepot, setSelectedDepot] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("");
  const [onlyLowStock, setOnlyLowStock] = useState(false);

  // Modals / forms
  const [showReceiveModal, setShowReceiveModal] = useState(false);
  const [receiveDepotId, setReceiveDepotId] = useState("");
  const [receiveItemId, setReceiveItemId] = useState("");
  const [receiveQty, setReceiveQty] = useState(50);
  const [receiveRef, setReceiveRef] = useState("");

  const [showItemModal, setShowItemModal] = useState(false);
  const [newItemCode, setNewItemCode] = useState("");
  const [newItemName, setNewItemName] = useState("");
  const [newItemCat, setNewItemCat] = useState("Food");
  const [newItemUom, setNewItemUom] = useState("Pack");
  const [newItemThreshold, setNewItemThreshold] = useState(50);

  const [showDepotModal, setShowDepotModal] = useState(false);
  const [newDepotCode, setNewDepotCode] = useState("");
  const [newDepotName, setNewDepotName] = useState("");
  const [newDepotDistrict, setNewDepotDistrict] = useState("Kalutara");
  const [newDepotAddress, setNewDepotAddress] = useState("");
  const [newDepotContact, setNewDepotContact] = useState("");
  const [newDepotPhone, setNewDepotPhone] = useState("");

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [sumRes, alertRes, stockRes, itemsRes, depotsRes, resRes] = await Promise.all([
        api.inventorySummary(),
        api.lowStockAlerts(),
        api.stockLevels(selectedDepot || undefined, onlyLowStock || undefined),
        api.supplyItems(selectedCategory || undefined),
        api.depots(),
        api.reservations(selectedDepot || undefined),
      ]);
      setSummary(sumRes);
      setAlerts(alertRes);
      setStock(stockRes);
      setItems(itemsRes);
      setDepots(depotsRes);
      setReservations(resRes);
    } catch (err: any) {
      setError(err?.message ?? "Failed to load inventory data.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [selectedDepot, selectedCategory, onlyLowStock]);

  const handleReceiveStock = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!receiveDepotId || !receiveItemId || receiveQty <= 0) return;
    try {
      await api.receiveStock({
        depotId: receiveDepotId,
        supplyItemId: receiveItemId,
        quantity: receiveQty,
        referenceNumber: receiveRef || undefined,
      });
      setShowReceiveModal(false);
      setActionMessage("Stock received and catalog updated successfully!");
      await loadData();
    } catch (err: any) {
      setError(err?.message ?? "Could not receive stock.");
    }
  };

  const handleCreateItem = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.createSupplyItem({
        code: newItemCode.trim(),
        name: newItemName.trim(),
        category: newItemCat,
        unitOfMeasure: newItemUom.trim(),
        reorderThreshold: newItemThreshold,
        standardFamilyAllocation: 1,
      });
      setShowItemModal(false);
      setNewItemCode("");
      setNewItemName("");
      setActionMessage("Supply catalog item created successfully!");
      await loadData();
    } catch (err: any) {
      setError(err?.message ?? "Could not create item.");
    }
  };

  const handleCreateDepot = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.createDepot({
        code: newDepotCode.trim(),
        name: newDepotName.trim(),
        district: newDepotDistrict,
        addressText: newDepotAddress.trim(),
        contactPerson: newDepotContact.trim(),
        contactPhone: newDepotPhone.trim(),
        capacitySquareMeters: 500,
      });
      setShowDepotModal(false);
      setNewDepotCode("");
      setNewDepotName("");
      setNewDepotAddress("");
      setNewDepotContact("");
      setNewDepotPhone("");
      setActionMessage("Depot registered successfully!");
      await loadData();
    } catch (err: any) {
      setError(err?.message ?? "Could not register depot.");
    }
  };

  const handleFulfillReservation = async (id: string) => {
    if (!window.confirm("Confirm fulfillment and dispatch of this stock reservation?")) return;
    try {
      await api.fulfillReservation(id, "Dispatched via web console");
      setActionMessage("Reservation fulfilled and stock dispatched.");
      await loadData();
    } catch (err: any) {
      setError(err?.message ?? "Could not fulfill reservation.");
    }
  };

  const handleCancelReservation = async (id: string) => {
    if (!window.confirm("Cancel this reservation and release stock back to the pool?")) return;
    try {
      await api.cancelReservation(id, "Cancelled by coordinator in console");
      setActionMessage("Reservation cancelled and stock released.");
      await loadData();
    } catch (err: any) {
      setError(err?.message ?? "Could not cancel reservation.");
    }
  };

  const filteredStock = stock.filter((s) => {
    if (selectedCategory && s.category !== selectedCategory) return false;
    if (search && !s.itemName.toLowerCase().includes(search.toLowerCase()) && !s.itemCode.toLowerCase().includes(search.toLowerCase()))
      return false;
    return true;
  });

  return (
    <div className="inventory-page">
      <header className="page-head">
        <div className="page-head-row">
          <div>
            <h1>Supply &amp; Inventory Management</h1>
            <p className="muted">Component B · Depots, Stock Levels, Low-Stock Warnings &amp; Atomic Reservations</p>
          </div>
          <div className="button-row">
            <button className="btn btn-primary" onClick={() => setShowReceiveModal(true)}>
              + Receive Stock Batch
            </button>
            <button className="btn btn-quiet" onClick={() => setShowItemModal(true)}>
              + New Supply Item
            </button>
            <button className="btn btn-quiet" onClick={() => setShowDepotModal(true)}>
              + Register Depot
            </button>
          </div>
        </div>
      </header>

      {actionMessage && (
        <div className="callout callout-ok" role="status" style={{ marginBottom: "1rem" }}>
          {actionMessage}
        </div>
      )}
      {error && (
        <div className="callout callout-fail" role="alert" style={{ marginBottom: "1rem" }}>
          {error}
        </div>
      )}

      {/* Summary KPI Cards */}
      {summary && (
        <section className="stats-grid" aria-label="Inventory metrics" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "0.8rem", marginBottom: "1.4rem" }}>
          <div className="card stat">
            <span className="stat-label">Total On Hand</span>
            <span className="stat-value">{summary.totalStockOnHand.toLocaleString()}</span>
          </div>
          <div className="card stat">
            <span className="stat-label">Reserved Units</span>
            <span className="stat-value" style={{ color: "var(--high)" }}>{summary.totalStockReserved.toLocaleString()}</span>
          </div>
          <div className="card stat">
            <span className="stat-label">Available Units</span>
            <span className="stat-value" style={{ color: "var(--signal)" }}>{summary.totalStockAvailable.toLocaleString()}</span>
          </div>
          <div className="card stat">
            <span className="stat-label">Depots</span>
            <span className="stat-value">{summary.totalDepots}</span>
          </div>
          <div className="card stat">
            <span className="stat-label">Catalog Items</span>
            <span className="stat-value">{summary.totalSupplyItems}</span>
          </div>
          <div className="card stat">
            <span className="stat-label">Low Stock Alerts</span>
            <span className="stat-value" style={{ color: summary.lowStockAlertsCount > 0 ? "var(--critical)" : "inherit" }}>
              {summary.lowStockAlertsCount}
            </span>
          </div>
        </section>
      )}

      {/* Low Stock Banner */}
      {alerts.length > 0 && (
        <div className="card" style={{ borderColor: "var(--critical)", background: "#fff6f5", marginBottom: "1.4rem" }}>
          <h2 style={{ color: "var(--critical)" }}>⚠️ Low Stock Warnings ({alerts.length})</h2>
          <p className="muted">Available stock has dropped to or below the safety reorder threshold.</p>
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.6rem" }}>
            {alerts.map((a) => (
              <span key={`${a.depotId}-${a.supplyItemId}`} className="pill" style={{ background: "var(--critical)", color: "#fff" }}>
                {a.depotName} ({a.district}): {a.itemName} — {a.quantityAvailable} available (Threshold: {a.reorderThreshold})
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: "flex", gap: "0.4rem", borderBottom: "2px solid var(--line)", marginBottom: "1.2rem" }}>
        <button
          className={`btn ${tab === "stock" ? "btn-primary" : "btn-quiet"}`}
          style={{ borderRadius: "6px 6px 0 0", borderBottom: "none" }}
          onClick={() => setTab("stock")}
        >
          Stock Levels ({filteredStock.length})
        </button>
        <button
          className={`btn ${tab === "reservations" ? "btn-primary" : "btn-quiet"}`}
          style={{ borderRadius: "6px 6px 0 0", borderBottom: "none" }}
          onClick={() => setTab("reservations")}
        >
          Active Reservations ({reservations.length})
        </button>
        <button
          className={`btn ${tab === "items" ? "btn-primary" : "btn-quiet"}`}
          style={{ borderRadius: "6px 6px 0 0", borderBottom: "none" }}
          onClick={() => setTab("items")}
        >
          Supply Catalog ({items.length})
        </button>
        <button
          className={`btn ${tab === "depots" ? "btn-primary" : "btn-quiet"}`}
          style={{ borderRadius: "6px 6px 0 0", borderBottom: "none" }}
          onClick={() => setTab("depots")}
        >
          Depots &amp; Hubs ({depots.length})
        </button>
      </div>

      {/* Tab 1: Stock Levels */}
      {tab === "stock" && (
        <section>
          <div className="filters" style={{ display: "flex", gap: "0.8rem", flexWrap: "wrap", marginBottom: "1rem" }}>
            <input
              type="search"
              placeholder="Search item code or name..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ flex: 1, minWidth: "200px" }}
            />
            <select value={selectedDepot} onChange={(e) => setSelectedDepot(e.target.value)} style={{ width: "200px" }}>
              <option value="">All Depots</option>
              {depots.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} ({d.district})
                </option>
              ))}
            </select>
            <select value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)} style={{ width: "160px" }}>
              <option value="">All Categories</option>
              {["Food", "Water", "Shelter", "Medical", "Clothing", "Sanitation"].map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
            <label className="check-inline" style={{ marginTop: "0.4rem" }}>
              <input type="checkbox" checked={onlyLowStock} onChange={(e) => setOnlyLowStock(e.target.checked)} />
              Low Stock Only
            </label>
          </div>

          {loading ? (
            <p className="muted">Loading stock levels...</p>
          ) : (
            <div className="table-wrap">
              <table className="table" style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr style={{ textAlign: "left", borderBottom: "2px solid var(--line)" }}>
                    <th>Item</th>
                    <th>Category</th>
                    <th>Depot</th>
                    <th>On Hand</th>
                    <th>Reserved</th>
                    <th>Available</th>
                    <th>Bin</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredStock.map((s) => (
                    <tr key={s.id} style={{ borderBottom: "1px solid var(--line)" }}>
                      <td>
                        <strong>{s.itemName}</strong>
                        <div className="muted" style={{ fontSize: "0.85rem" }}>{s.itemCode}</div>
                      </td>
                      <td><span className="pill">{s.category}</span></td>
                      <td>{s.depotName} <span className="muted">({s.depotDistrict})</span></td>
                      <td>{s.quantityOnHand} {s.unitOfMeasure}</td>
                      <td style={{ color: "var(--high)" }}>{s.quantityReserved}</td>
                      <td>
                        <strong style={{ color: s.isLowStock ? "var(--critical)" : "var(--signal)" }}>
                          {s.quantityAvailable} {s.unitOfMeasure}
                        </strong>
                      </td>
                      <td>{s.storageBin || "—"}</td>
                      <td>
                        {s.isLowStock ? (
                          <span className="pill" style={{ background: "var(--critical)", color: "#fff" }}>Low Stock</span>
                        ) : (
                          <span className="pill" style={{ background: "#e8f5e9", color: "#2e7d32" }}>Adequate</span>
                        )}
                      </td>
                    </tr>
                  ))}
                  {filteredStock.length === 0 && (
                    <tr>
                      <td colSpan={8} style={{ textAlign: "center", padding: "1.5rem" }} className="muted">
                        No inventory matching the current search/filter.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* Tab 2: Reservations */}
      {tab === "reservations" && (
        <section>
          <div className="table-wrap">
            <table className="table" style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "2px solid var(--line)" }}>
                  <th>Reservation #</th>
                  <th>Item</th>
                  <th>Depot</th>
                  <th>Quantity</th>
                  <th>Status</th>
                  <th>Expires (UTC)</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {reservations.map((r) => (
                  <tr key={r.id} style={{ borderBottom: "1px solid var(--line)" }}>
                    <td><code>{r.reservationNumber}</code></td>
                    <td><strong>{r.itemName}</strong></td>
                    <td>{r.depotName}</td>
                    <td><strong>{r.quantity}</strong> units</td>
                    <td>
                      <span className={`pill ${r.status === "Allocated" ? "pill-open" : ""}`}>{r.status}</span>
                    </td>
                    <td>{new Date(r.expiresAt).toLocaleString()}</td>
                    <td>
                      {r.status === "Allocated" && (
                        <div style={{ display: "flex", gap: "0.4rem" }}>
                          <button className="btn btn-primary" style={{ padding: "0.2rem 0.6rem", minHeight: "1.8rem", fontSize: "0.85rem" }} onClick={() => handleFulfillReservation(r.id)}>
                            Fulfill &amp; Dispatch
                          </button>
                          <button className="btn btn-danger" style={{ padding: "0.2rem 0.6rem", minHeight: "1.8rem", fontSize: "0.85rem" }} onClick={() => handleCancelReservation(r.id)}>
                            Cancel
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
                {reservations.length === 0 && (
                  <tr>
                    <td colSpan={7} style={{ textAlign: "center", padding: "1.5rem" }} className="muted">
                      No active reservations.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Tab 3: Catalog Items */}
      {tab === "items" && (
        <section>
          <div className="table-wrap">
            <table className="table" style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "2px solid var(--line)" }}>
                  <th>Code</th>
                  <th>Item Name</th>
                  <th>Category</th>
                  <th>Unit</th>
                  <th>Reorder Threshold</th>
                  <th>Family Alloc</th>
                </tr>
              </thead>
              <tbody>
                {items.map((i) => (
                  <tr key={i.id} style={{ borderBottom: "1px solid var(--line)" }}>
                    <td><code>{i.code}</code></td>
                    <td>
                      <strong>{i.name}</strong>
                      {i.description && <div className="muted" style={{ fontSize: "0.85rem" }}>{i.description}</div>}
                    </td>
                    <td><span className="pill">{i.category}</span></td>
                    <td>{i.unitOfMeasure}</td>
                    <td>{i.reorderThreshold}</td>
                    <td>{i.standardFamilyAllocation} / family</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Tab 4: Depots */}
      {tab === "depots" && (
        <section>
          <div className="table-wrap">
            <table className="table" style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "2px solid var(--line)" }}>
                  <th>Code</th>
                  <th>Depot Name</th>
                  <th>District</th>
                  <th>Address</th>
                  <th>Contact Person</th>
                  <th>Phone</th>
                </tr>
              </thead>
              <tbody>
                {depots.map((d) => (
                  <tr key={d.id} style={{ borderBottom: "1px solid var(--line)" }}>
                    <td><code>{d.code}</code></td>
                    <td><strong>{d.name}</strong></td>
                    <td>{d.district}</td>
                    <td>{d.addressText}</td>
                    <td>{d.contactPerson}</td>
                    <td>{d.contactPhone}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Receive Stock Modal */}
      {showReceiveModal && (
        <div className="modal-backdrop" style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)", display: "grid", placeItems: "center", zIndex: 999 }}>
          <div className="card" style={{ maxWidth: "500px", width: "90%", background: "#fff", padding: "1.5rem" }}>
            <h2>Receive Stock Shipment</h2>
            <form onSubmit={handleReceiveStock}>
              <div className="stacked">
                <label>Depot</label>
                <select value={receiveDepotId} onChange={(e) => setReceiveDepotId(e.target.value)} required>
                  <option value="">Select Depot...</option>
                  {depots.map((d) => (
                    <option key={d.id} value={d.id}>{d.name} ({d.district})</option>
                  ))}
                </select>
              </div>
              <div className="stacked">
                <label>Supply Item</label>
                <select value={receiveItemId} onChange={(e) => setReceiveItemId(e.target.value)} required>
                  <option value="">Select Item...</option>
                  {items.map((i) => (
                    <option key={i.id} value={i.id}>{i.name} ({i.code})</option>
                  ))}
                </select>
              </div>
              <div className="stacked">
                <label>Quantity Received</label>
                <input type="number" min="1" value={receiveQty} onChange={(e) => setReceiveQty(Number(e.target.value))} required />
              </div>
              <div className="stacked">
                <label>Reference # (PO or Donation ID)</label>
                <input type="text" placeholder="e.g. PO-2026-10-88" value={receiveRef} onChange={(e) => setReceiveRef(e.target.value)} />
              </div>
              <div className="button-row" style={{ justifyContent: "flex-end", marginTop: "1.2rem" }}>
                <button type="button" className="btn btn-quiet" onClick={() => setShowReceiveModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Receive Stock</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New Item Modal */}
      {showItemModal && (
        <div className="modal-backdrop" style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)", display: "grid", placeItems: "center", zIndex: 999 }}>
          <div className="card" style={{ maxWidth: "500px", width: "90%", background: "#fff", padding: "1.5rem" }}>
            <h2>Create Supply Catalog Item</h2>
            <form onSubmit={handleCreateItem}>
              <div className="stacked">
                <label>SKU / Item Code</label>
                <input type="text" placeholder="SKU-WTR-5L" value={newItemCode} onChange={(e) => setNewItemCode(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Item Name</label>
                <input type="text" placeholder="Bottled Drinking Water (5L)" value={newItemName} onChange={(e) => setNewItemName(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Category</label>
                <select value={newItemCat} onChange={(e) => setNewItemCat(e.target.value)}>
                  {["Food", "Water", "Shelter", "Medical", "Clothing", "Sanitation"].map((c) => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>
              </div>
              <div className="stacked">
                <label>Unit of Measure</label>
                <input type="text" value={newItemUom} onChange={(e) => setNewItemUom(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Low Stock Reorder Threshold</label>
                <input type="number" min="0" value={newItemThreshold} onChange={(e) => setNewItemThreshold(Number(e.target.value))} required />
              </div>
              <div className="button-row" style={{ justifyContent: "flex-end", marginTop: "1.2rem" }}>
                <button type="button" className="btn btn-quiet" onClick={() => setShowItemModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Save Item</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* New Depot Modal */}
      {showDepotModal && (
        <div className="modal-backdrop" style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)", display: "grid", placeItems: "center", zIndex: 999 }}>
          <div className="card" style={{ maxWidth: "500px", width: "90%", background: "#fff", padding: "1.5rem" }}>
            <h2>Register New Depot</h2>
            <form onSubmit={handleCreateDepot}>
              <div className="stacked">
                <label>Depot Code</label>
                <input type="text" placeholder="DEP-GAL-01" value={newDepotCode} onChange={(e) => setNewDepotCode(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Depot Name</label>
                <input type="text" placeholder="Galle Emergency Logistics Center" value={newDepotName} onChange={(e) => setNewDepotName(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>District</label>
                <input type="text" value={newDepotDistrict} onChange={(e) => setNewDepotDistrict(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Address</label>
                <input type="text" placeholder="Port Road, Galle" value={newDepotAddress} onChange={(e) => setNewDepotAddress(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Contact Person</label>
                <input type="text" placeholder="Depot Manager Name" value={newDepotContact} onChange={(e) => setNewDepotContact(e.target.value)} required />
              </div>
              <div className="stacked">
                <label>Contact Phone</label>
                <input type="text" placeholder="0912233445" value={newDepotPhone} onChange={(e) => setNewDepotPhone(e.target.value)} required />
              </div>
              <div className="button-row" style={{ justifyContent: "flex-end", marginTop: "1.2rem" }}>
                <button type="button" className="btn btn-quiet" onClick={() => setShowDepotModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Save Depot</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
