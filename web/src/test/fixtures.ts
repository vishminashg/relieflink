import type {
  ReliefRequest,
  Volunteer,
  VolunteerAssignment,
  VolunteerMatchResult,
  VolunteerSummary,
  WorkflowDetail,
  WorkflowStatus,
} from "../api/types";

export function makeRequest(over: Partial<ReliefRequest> = {}): ReliefRequest {
  return {
    id: "r1", requester: { id: "u1", fullName: "Nimali Perera", phone: "0771234567" },
    householdSize: 5, elderlyCount: 1, childrenCount: 2, homeFlooded: true,
    district: "Gampaha", addressText: "12 Temple Road, Kelaniya", latitude: 6.95, longitude: 79.92,
    notes: "Water is rising", photoUrl: null, neededBy: null,
    status: "Submitted", triageScore: 82, urgency: "Critical",
    urgencyOverridden: false, urgencyOverrideReason: null,
    triageReasons: ["Home is flooded", "2 children in the household"], cancellationReason: null,
    items: [{ id: "i1", category: "Water", quantity: 20, description: "bottles" }],
    createdAt: "2026-10-13T06:00:00Z", updatedAt: "2026-10-13T06:00:00Z", ...over,
  };
}

export function makeWorkflow(status: WorkflowStatus, over: Partial<WorkflowDetail> = {}): WorkflowDetail {
  const plan = {
    summary: "Deliver water and food within 6 hours.", recommendedUrgency: "Critical", targetResponseHours: 6,
    needs: [{ category: "Water", quantity: 20, action: "Dispatch from the nearest depot" }],
    rationale: ["Flooded home with children"], risks: ["Road may be blocked"],
  };
  return {
    workflow: {
      id: "w1", reliefRequestId: "r1", objective: "Plan relief for Gampaha", status,
      revisionCount: 0, retryCount: 0, failureReason: null,
      createdAt: "2026-10-13T07:00:00Z", updatedAt: "2026-10-13T07:05:00Z",
    },
    plan, proposal: null,
    steps: [{
      sequence: 1, agentName: "Planner", goal: "Draft a plan", status: "Completed", input: null, output: null,
      error: null, startedAt: null, completedAt: null, durationMs: 120,
      toolCalls: [{ toolName: "get_request", arguments: { id: "r1" }, result: { ok: true }, succeeded: true, durationMs: 5 }],
    }],
    validations: [
      { attempt: 1, checkName: "needs-covered", passed: false, detail: "Water missing" },
      { attempt: 2, checkName: "needs-covered", passed: true, detail: null },
    ],
    decisions: [], ...over,
  };
}

export function makeVolunteer(over: Partial<Volunteer> = {}): Volunteer {
  return {
    id: "v1",
    user: { id: "u2", fullName: "Sunil Fernando", email: "sunil@relief.lk", phone: "0712345678" },
    district: "Colombo",
    emergencyContactName: "Kamala Fernando",
    emergencyContactPhone: "0779988776",
    isActive: true,
    maxHoursPerWeek: 30,
    notes: "Experienced first responder with 4x4",
    skills: [
      { id: "s1", skill: "FirstAid", proficiency: "Advanced", certificateReference: "SLRC-FA-2025", isVerified: true },
      { id: "s2", skill: "SearchAndRescue", proficiency: "Intermediate", certificateReference: null, isVerified: false },
    ],
    availabilities: [
      { id: "a1", type: "RecurringWeekly", dayOfWeek: "Monday", startTime: "08:00:00", endTime: "16:00:00", specificStartUtc: null, specificEndUtc: null },
    ],
    activeAssignmentsCount: 0,
    createdAt: "2026-10-10T10:00:00Z",
    updatedAt: "2026-10-10T10:00:00Z",
    ...over,
  };
}

export function makeAssignment(over: Partial<VolunteerAssignment> = {}): VolunteerAssignment {
  return {
    id: "asg-1",
    volunteerId: "v1",
    volunteerName: "Sunil Fernando",
    reliefRequestId: "r1",
    startTime: "2026-10-20T08:00:00Z",
    endTime: "2026-10-20T14:00:00Z",
    status: "Assigned",
    roleOrTask: "First Aid & Medical Evacuation",
    notes: "Report to Kelaniya depot",
    createdByUserId: "s1",
    createdAt: "2026-10-18T09:00:00Z",
    updatedAt: "2026-10-18T09:00:00Z",
    ...over,
  };
}

export function makeMatchResult(over: Partial<VolunteerMatchResult> = {}): VolunteerMatchResult {
  return {
    volunteer: makeVolunteer(),
    score: 85,
    quality: "Good",
    isEligible: true,
    hasScheduleConflict: false,
    hasAvailability: true,
    reasons: ["Exact district match (+30)", "Has certified skills: FirstAid (+40)"],
    matchedSkills: ["FirstAid"],
    missingSkills: [],
    ...over,
  };
}

export function makeVolunteerSummary(over: Partial<VolunteerSummary> = {}): VolunteerSummary {
  return {
    totalVolunteers: 45,
    activeVolunteers: 38,
    currentlyAssignedVolunteers: 12,
    availableVolunteers: 26,
    byDistrict: { Colombo: 20, Gampaha: 15, Kalutara: 10 },
    bySkill: { FirstAid: 25, SearchAndRescue: 12, BoatOperation: 6 },
    assignmentsByStatus: { Assigned: 4, InProgress: 8, Completed: 30, Cancelled: 3 },
    ...over,
  };
}

/** A fetch stand-in that answers by "METHOD path" and records every call. */
export function mockFetch(routes: Record<string, unknown | ((body: unknown) => unknown)>) {
  const calls: { key: string; body: unknown }[] = [];
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input));
    const key = `${init?.method ?? "GET"} ${url.pathname}`;
    const body = init?.body ? JSON.parse(String(init.body)) : undefined;
    calls.push({ key, body });
    const route = routes[key] ?? routes[`${key}${url.search}`];
    if (route === undefined) return new Response(JSON.stringify({ title: `No mock for ${key}` }), { status: 404 });
    const value = typeof route === "function" ? (route as (b: unknown) => unknown)(body) : route;
    if (value instanceof Response) return value;
    return new Response(JSON.stringify(value), { status: 200, headers: { "Content-Type": "application/json" } });
  });
  vi.stubGlobal("fetch", fn);
  return { fn, calls };
}

export const paged = <T,>(items: T[]) => ({ items, page: 1, pageSize: 15, totalCount: items.length, totalPages: 1 });

export const staffSession = {
  token: "t", expiresAtUtc: "2999-01-01T00:00:00Z",
  user: { id: "s1", fullName: "Coordinator One", email: "c@x.lk", role: "Coordinator", isActive: true },
};
