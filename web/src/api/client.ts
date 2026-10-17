import type {
  AuthResponse, Paged, ReliefRequest, RequestFilters, Summary, WorkflowDetail, WorkflowStatus, WorkflowSummary,
} from "./types";

export const API_URL = (import.meta.env.VITE_API_URL ?? "http://localhost:5039").replace(/\/$/, "");

/** A readable error built from the API's problem-details response. */
export class ApiError extends Error {
  constructor(public status: number, message: string, public fields: Record<string, string[]> = {}) {
    super(message);
  }
}

let token: string | null = null;
let onUnauthorized: () => void = () => {};

export function setToken(value: string | null) { token = value; }
export function setUnauthorizedHandler(handler: () => void) { onUnauthorized = handler; }

async function call<T>(method: string, path: string, body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers: {
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError(0, "Cannot reach the server. Check your connection and that the API is running.");
  }

  if (response.status === 401 && token) onUnauthorized(); // an expired or revoked token ends the session

  if (!response.ok) {
    let title = `The server answered ${response.status}.`;
    let fields: Record<string, string[]> = {};
    try {
      const problem = await response.json();
      if (problem?.title) title = problem.title;
      if (problem?.errors) fields = problem.errors;
    } catch { /* not JSON: keep the generic message */ }
    if (response.status === 401 && token) title = "Your session has ended. Sign in again."; // a failed login keeps the server's own message
    if (response.status === 403) title = "Your role is not allowed to do this.";
    throw new ApiError(response.status, title, fields);
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

const query = (params: Record<string, string | number | undefined>) => {
  const q = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== "") q.set(k, String(v)); });
  const s = q.toString();
  return s ? `?${s}` : "";
};

export const api = {
  login: (email: string, password: string) => call<AuthResponse>("POST", "/api/auth/login", { email, password }),

  summary: () => call<Summary>("GET", "/api/relief-requests/summary"),
  requests: (f: RequestFilters = {}) => call<Paged<ReliefRequest>>("GET", `/api/relief-requests${query({ ...f })}`),
  request: (id: string) => call<ReliefRequest>("GET", `/api/relief-requests/${id}`),
  overrideUrgency: (id: string, urgency: string, reason: string) =>
    call<ReliefRequest>("POST", `/api/relief-requests/${id}/triage`, { urgency, reason }),
  recomputeTriage: (id: string) => call<ReliefRequest>("POST", `/api/relief-requests/${id}/triage`, {}),
  cancelRequest: (id: string, reason: string) =>
    call<ReliefRequest>("POST", `/api/relief-requests/${id}/cancel`, { reason }),

  startWorkflow: (reliefRequestId: string) => call<WorkflowDetail>("POST", "/api/workflows", { reliefRequestId }),
  workflows: (f: { status?: WorkflowStatus | ""; reliefRequestId?: string; page?: number; pageSize?: number } = {}) =>
    call<Paged<WorkflowSummary>>("GET", `/api/workflows${query({ ...f })}`),
  workflow: (id: string) => call<WorkflowDetail>("GET", `/api/workflows/${id}`),
  approve: (id: string, comment?: string) => call<WorkflowDetail>("POST", `/api/workflows/${id}/approve`, { comment }),
  reject: (id: string, comment: string, rejectRequest: boolean) =>
    call<WorkflowDetail>("POST", `/api/workflows/${id}/reject`, { comment, rejectRequest }),
  revise: (id: string, comment: string) => call<WorkflowDetail>("POST", `/api/workflows/${id}/revise`, { comment }),
  execute: (id: string) => call<WorkflowDetail>("POST", `/api/workflows/${id}/execute`),

  // Component B: Supply and Inventory Management
  depots: (district?: string, search?: string) =>
    call<import("./types").Depot[]>("GET", `/api/depots${query({ district, search })}`),
  depot: (id: string) => call<import("./types").Depot>("GET", `/api/depots/${id}`),
  createDepot: (data: Partial<import("./types").Depot>) => call<import("./types").Depot>("POST", "/api/depots", data),
  updateDepot: (id: string, data: Partial<import("./types").Depot>) => call<import("./types").Depot>("PUT", `/api/depots/${id}`, data),
  supplyItems: (category?: string, search?: string) =>
    call<import("./types").SupplyItem[]>("GET", `/api/supply-items${query({ category, search })}`),
  createSupplyItem: (data: Partial<import("./types").SupplyItem>) => call<import("./types").SupplyItem>("POST", "/api/supply-items", data),
  stockLevels: (depotId?: string, isLowStock?: boolean) =>
    call<import("./types").StockLevel[]>("GET", `/api/stock${query({ depotId, isLowStock: isLowStock ? "true" : undefined })}`),
  inventorySummary: () => call<import("./types").InventorySummary>("GET", "/api/inventory/summary"),
  lowStockAlerts: () => call<import("./types").LowStockAlert[]>("GET", "/api/inventory/low-stock"),
  receiveStock: (data: { depotId: string; supplyItemId: string; quantity: number; referenceNumber?: string; notes?: string }) =>
    call<import("./types").StockLevel>("POST", "/api/stock/receive", data),
  reservations: (depotId?: string, status?: string) =>
    call<import("./types").StockReservation[]>("GET", `/api/reservations${query({ depotId, status })}`),
  createReservation: (data: { depotId: string; supplyItemId: string; quantity: number; notes?: string }) =>
    call<import("./types").StockReservation>("POST", "/api/reservations", data),
  fulfillReservation: (id: string, notes?: string) =>
    call<import("./types").StockReservation>("POST", `/api/reservations/${id}/fulfill${query({ notes })}`),
  cancelReservation: (id: string, reason?: string) =>
    call<import("./types").StockReservation>("POST", `/api/reservations/${id}/cancel${query({ reason })}`),
};
