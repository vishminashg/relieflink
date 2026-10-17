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

// ==========================================
// Component C: Volunteer Management
// ==========================================

export type VolunteerSkillType =
  | "FirstAid"
  | "Medical"
  | "SearchAndRescue"
  | "BoatOperation"
  | "HeavyVehicleDriving"
  | "FoodDistribution"
  | "ChildCare"
  | "ElderlyCare";

export type SkillProficiency = "Beginner" | "Intermediate" | "Advanced" | "Expert";
export type AvailabilityType = "RecurringWeekly" | "SpecificDate";
export type AssignmentStatus = "Assigned" | "InProgress" | "Completed" | "Cancelled";

export interface VolunteerSkill {
  id: string;
  skill: VolunteerSkillType;
  proficiency: SkillProficiency;
  certificateReference: string | null;
  isVerified: boolean;
}

export interface VolunteerAvailability {
  id: string;
  type: AvailabilityType;
  dayOfWeek: string | null;
  startTime: string | null;
  endTime: string | null;
  specificStartUtc: string | null;
  specificEndUtc: string | null;
}

export interface VolunteerUser {
  id: string;
  fullName: string;
  email: string;
  phone: string | null;
}

export interface Volunteer {
  id: string;
  user: VolunteerUser | null;
  district: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
  isActive: boolean;
  maxHoursPerWeek: number;
  notes: string | null;
  skills: VolunteerSkill[];
  availabilities: VolunteerAvailability[];
  activeAssignmentsCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface VolunteerMatchResult {
  volunteer: Volunteer;
  score: number;
  quality: "Low" | "Adequate" | "Good" | "Excellent";
  isEligible: boolean;
  hasScheduleConflict: boolean;
  hasAvailability: boolean;
  reasons: string[];
  matchedSkills: string[];
  missingSkills: string[];
}

export interface VolunteerAgentCheck {
  name: string;
  passed: boolean;
  detail: string | null;
}

export interface VolunteerAgentProposal {
  volunteerIds: string[];
  skillsCovered: string[];
  summary: string;
  rationale: string[];
}

export interface VolunteerAgentMatchResponse {
  proposal: VolunteerAgentProposal;
  checks: VolunteerAgentCheck[];
  toolCalls: ToolCall[];
}

export interface VolunteerAssignment {
  id: string;
  volunteerId: string;
  volunteerName: string;
  reliefRequestId: string | null;
  startTime: string;
  endTime: string;
  status: AssignmentStatus;
  roleOrTask: string | null;
  notes: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

export interface VolunteerSummary {
  totalVolunteers: number;
  activeVolunteers: number;
  currentlyAssignedVolunteers: number;
  availableVolunteers: number;
  byDistrict: Record<string, number>;
  bySkill: Record<string, number>;
  assignmentsByStatus: Record<string, number>;
}

export interface VolunteerFilters {
  district?: string;
  skill?: VolunteerSkillType | "";
  isActive?: boolean | "";
  search?: string;
  sort?: "createdAt_desc" | "createdAt_asc" | "name_asc" | "name_desc" | "district_asc";
  page?: number;
  pageSize?: number;
}

