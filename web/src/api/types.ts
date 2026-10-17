// These mirror the JSON the ASP.NET Core API returns (camelCase, enums as strings).

export type Role = "Citizen" | "Volunteer" | "Coordinator" | "Admin";
export type Urgency = "Low" | "Medium" | "High" | "Critical";
export type RequestStatus =
  | "Submitted" | "UnderReview" | "PlanReady" | "Approved" | "InProgress" | "Fulfilled" | "Rejected" | "Cancelled";
export type WorkflowStatus =
  | "Submitted" | "Planning" | "Proposed" | "Validating" | "PendingCoordinatorApproval"
  | "Approved" | "Executed" | "RevisionRequested" | "Rejected" | "FailedSafe";

export interface User { id: string; fullName: string; email: string; role: Role; isActive: boolean }
export interface AuthResponse { token: string; expiresAtUtc: string; user: User }

export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }

export interface NeedItem { id: string; category: string; quantity: number; description: string | null }

export interface ReliefRequest {
  id: string;
  requester: { id: string; fullName: string; phone: string | null } | null;
  householdSize: number; elderlyCount: number; childrenCount: number; homeFlooded: boolean;
  district: string; addressText: string; latitude: number; longitude: number;
  notes: string | null; photoUrl: string | null; neededBy: string | null;
  status: RequestStatus; triageScore: number; urgency: Urgency;
  urgencyOverridden: boolean; urgencyOverrideReason: string | null;
  triageReasons: string[]; cancellationReason: string | null;
  items: NeedItem[]; createdAt: string; updatedAt: string;
}

export interface Summary {
  total: number; open: number; criticalOpen: number;
  byStatus: Record<string, number>; byUrgency: Record<string, number>; byDistrict: Record<string, number>;
}

export interface WorkflowSummary {
  id: string; reliefRequestId: string | null; objective: string; status: WorkflowStatus;
  revisionCount: number; retryCount: number; failureReason: string | null; createdAt: string; updatedAt: string;
}

export interface PlanLine { category: string; quantity: number; action: string }
export interface Plan {
  summary: string; recommendedUrgency: string; targetResponseHours: number;
  needs: PlanLine[]; rationale: string[]; risks: string[];
}
export interface ToolCall { toolName: string; arguments: unknown; result: unknown; succeeded: boolean; durationMs: number | null }
export interface WorkflowStep {
  sequence: number; agentName: string; goal: string; status: string; input: unknown; output: unknown;
  error: string | null; startedAt: string | null; completedAt: string | null; durationMs: number | null;
  toolCalls: ToolCall[];
}
export interface Validation { attempt: number; checkName: string; passed: boolean; detail: string | null }
export interface Decision { decidedByUserId: string; decidedByName: string | null; decision: string; comment: string | null; createdAt: string }

export interface WorkflowDetail {
  workflow: WorkflowSummary; plan: Plan | null; proposal: Plan | null;
  steps: WorkflowStep[]; validations: Validation[]; decisions: Decision[];
}

export interface RequestFilters {
  status?: RequestStatus | ""; urgency?: Urgency | ""; district?: string; search?: string;
  sort?: "urgency" | "newest" | "oldest"; page?: number; pageSize?: number;
}

// Component B: Supply and Inventory Management
export interface Depot {
  id: string;
  code: string;
  name: string;
  district: string;
  addressText: string;
  latitude: number;
  longitude: number;
  contactPerson: string;
  contactPhone: string;
  capacitySquareMeters: number;
  isActive: boolean;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SupplyItem {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: string;
  unitOfMeasure: string;
  weightKg: number;
  reorderThreshold: number;
  standardFamilyAllocation: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface StockLevel {
  id: string;
  depotId: string;
  depotCode: string;
  depotName: string;
  depotDistrict: string;
  supplyItemId: string;
  itemCode: string;
  itemName: string;
  category: string;
  unitOfMeasure: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  isLowStock: boolean;
  lastRestockedAt: string | null;
  storageBin: string | null;
}

export interface StockReservation {
  id: string;
  reservationNumber: string;
  depotId: string;
  depotName: string;
  supplyItemId: string;
  itemName: string;
  quantity: number;
  reliefRequestId: string | null;
  status: string;
  reservedByUserId: string;
  expiresAt: string;
  fulfilledAt: string | null;
  cancelledAt: string | null;
  notes: string | null;
  createdAt: string;
}

export interface LowStockAlert {
  depotId: string;
  depotName: string;
  district: string;
  supplyItemId: string;
  itemCode: string;
  itemName: string;
  category: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  reorderThreshold: number;
  shortage: number;
}

export interface InventorySummary {
  totalDepots: number;
  totalSupplyItems: number;
  totalStockOnHand: number;
  totalStockReserved: number;
  totalStockAvailable: number;
  lowStockAlertsCount: number;
  activeReservationsCount: number;
}

