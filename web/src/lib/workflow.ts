import type { WorkflowStatus } from "../api/types";

/** The normal route a workflow takes. Rejected, revision and failed-safe are side exits. */
export const PATH: { status: WorkflowStatus; label: string }[] = [
  { status: "Planning", label: "Planning" },
  { status: "Proposed", label: "Plan proposed" },
  { status: "Validating", label: "Checking" },
  { status: "PendingCoordinatorApproval", label: "Your decision" },
  { status: "Approved", label: "Approved" },
  { status: "Executed", label: "Executed" },
];

export const STATUS_LABEL: Record<string, string> = {
  Submitted: "Submitted",
  Planning: "Planning",
  Proposed: "Plan proposed",
  Validating: "Checking the plan",
  PendingCoordinatorApproval: "Waiting for your decision",
  Approved: "Approved",
  Executed: "Executed",
  RevisionRequested: "Revision requested",
  Rejected: "Rejected",
  FailedSafe: "Stopped safely",
  UnderReview: "Under review",
  PlanReady: "Plan ready",
  InProgress: "In progress",
  Fulfilled: "Fulfilled",
  Cancelled: "Cancelled",
};

export const statusLabel = (status: string) => STATUS_LABEL[status] ?? status;

export interface Actions { approve: boolean; revise: boolean; reject: boolean; execute: boolean }

/** Which buttons make sense. The API enforces the same rules; this only avoids offering impossible actions. */
export function availableActions(status: WorkflowStatus): Actions {
  const waiting = status === "PendingCoordinatorApproval";
  return { approve: waiting, revise: waiting, reject: waiting, execute: status === "Approved" };
}

export type StepState = "done" | "current" | "todo";

/** Where each stage on the path is for the given status. Side exits mark the path as stopped. */
export function pathStates(status: WorkflowStatus): { label: string; state: StepState }[] {
  const index = PATH.findIndex((p) => p.status === status);
  const stopped = status === "FailedSafe" || status === "Rejected" || status === "RevisionRequested";
  return PATH.map((p, i) => ({
    label: p.label,
    state: stopped ? "todo" : index === -1 ? "todo" : status === "Executed" || i < index ? "done" : i === index ? "current" : "todo",
  }));
}

export const isFinished = (status: WorkflowStatus) =>
  status === "Executed" || status === "Rejected" || status === "FailedSafe";
