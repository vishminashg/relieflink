import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockFetch } from "../test/fixtures";
import { renderApp } from "../test/render";

const mockSummary = {
  totalDepots: 2,
  totalSupplyItems: 3,
  totalStockOnHand: 1500,
  totalStockReserved: 100,
  totalStockAvailable: 1400,
  lowStockAlertsCount: 1,
  activeReservationsCount: 1,
};

const mockAlerts = [
  {
    depotId: "dep-1",
    depotName: "Kalutara Depot",
    district: "Kalutara",
    supplyItemId: "item-1",
    itemCode: "SKU-WTR-5L",
    itemName: "Bottled Drinking Water (5L)",
    category: "Water",
    quantityOnHand: 40,
    quantityReserved: 10,
    quantityAvailable: 30,
    reorderThreshold: 50,
    shortage: 20,
  },
];

const mockStock = [
  {
    id: "stock-1",
    depotId: "dep-1",
    depotCode: "DEP-KLU-01",
    depotName: "Kalutara Depot",
    depotDistrict: "Kalutara",
    supplyItemId: "item-1",
    itemCode: "SKU-WTR-5L",
    itemName: "Bottled Drinking Water (5L)",
    category: "Water",
    unitOfMeasure: "Bottle",
    quantityOnHand: 40,
    quantityReserved: 10,
    quantityAvailable: 30,
    isLowStock: true,
    lastRestockedAt: "2026-10-15T10:00:00Z",
    storageBin: "A-01",
  },
];

const mockDepots = [
  {
    id: "dep-1",
    code: "DEP-KLU-01",
    name: "Kalutara Depot",
    district: "Kalutara",
    addressText: "Main St, Kalutara",
    latitude: 6.58,
    longitude: 79.96,
    contactPerson: "Sunil Silva",
    contactPhone: "0771234567",
    capacitySquareMeters: 500,
    isActive: true,
    notes: null,
    createdAt: "2026-10-10T00:00:00Z",
    updatedAt: "2026-10-10T00:00:00Z",
  },
];

const mockItems = [
  {
    id: "item-1",
    code: "SKU-WTR-5L",
    name: "Bottled Drinking Water (5L)",
    description: "Clean drinking water",
    category: "Water",
    unitOfMeasure: "Bottle",
    weightKg: 5,
    reorderThreshold: 50,
    standardFamilyAllocation: 4,
    isActive: true,
    createdAt: "2026-10-10T00:00:00Z",
    updatedAt: "2026-10-10T00:00:00Z",
  },
];

const mockReservations = [
  {
    id: "res-1",
    reservationNumber: "RES-20261017-001",
    depotId: "dep-1",
    depotName: "Kalutara Depot",
    supplyItemId: "item-1",
    itemName: "Bottled Drinking Water (5L)",
    quantity: 10,
    reliefRequestId: null,
    status: "Allocated",
    reservedByUserId: "user-1",
    expiresAt: "2026-10-19T10:00:00Z",
    fulfilledAt: null,
    cancelledAt: null,
    notes: "Mission allocation",
    createdAt: "2026-10-17T10:00:00Z",
  },
];

function setupMocks() {
  return mockFetch({
    "GET /api/inventory/summary": mockSummary,
    "GET /api/inventory/low-stock": mockAlerts,
    "GET /api/stock": mockStock,
    "GET /api/supply-items": mockItems,
    "GET /api/depots": mockDepots,
    "GET /api/reservations": mockReservations,
  });
}

describe("Inventory Page", () => {
  it("renders inventory summary metrics and low stock alerts", async () => {
    setupMocks();
    renderApp("/inventory");

    expect(await screen.findByText("Supply & Inventory Management")).toBeInTheDocument();
    expect(await screen.findByText("1,500")).toBeInTheDocument(); // Total On Hand
    expect(await screen.findByText("1,400")).toBeInTheDocument(); // Available Units
    expect(screen.getByText("⚠️ Low Stock Warnings (1)")).toBeInTheDocument();
  });

  it("switches tabs between stock, reservations, catalog, and depots", async () => {
    setupMocks();
    renderApp("/inventory");

    const user = userEvent.setup();

    // On stock tab by default
    expect(await screen.findByText("Bottled Drinking Water (5L)")).toBeInTheDocument();

    // Click reservations tab
    await user.click(screen.getByRole("button", { name: /Active Reservations/i }));
    expect(await screen.findByText("RES-20261017-001")).toBeInTheDocument();

    // Click depots tab
    await user.click(screen.getByRole("button", { name: /Depots & Hubs/i }));
    expect(await screen.findByText("Main St, Kalutara")).toBeInTheDocument();
  });
});
