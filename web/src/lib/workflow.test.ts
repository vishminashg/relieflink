import { availableActions, isFinished, pathStates, statusLabel } from "./workflow";

describe("availableActions", () => {
  it("offers approve, revise and reject only while waiting for a decision", () => {
    expect(availableActions("PendingCoordinatorApproval")).toEqual({ approve: true, revise: true, reject: true, execute: false });
  });
  it("offers execute only once approved", () => {
    expect(availableActions("Approved")).toEqual({ approve: false, revise: false, reject: false, execute: true });
  });
  it.each(["Planning", "Proposed", "Validating", "Executed", "Rejected", "FailedSafe", "RevisionRequested", "Submitted"] as const)(
    "offers nothing while %s", (status) => {
      expect(availableActions(status)).toEqual({ approve: false, revise: false, reject: false, execute: false });
    });
});

describe("pathStates", () => {
  it("marks earlier stages done and the current one current", () => {
    const states = pathStates("Validating").map((s) => s.state);
    expect(states).toEqual(["done", "done", "current", "todo", "todo", "todo"]);
  });
  it("marks everything done once executed", () => {
    expect(pathStates("Executed").every((s) => s.state === "done")).toBe(true);
  });
  it("shows nothing as current when the workflow stopped", () => {
    for (const status of ["FailedSafe", "Rejected", "RevisionRequested"] as const) {
      expect(pathStates(status).some((s) => s.state === "current")).toBe(false);
    }
  });
});

describe("labels and finished states", () => {
  it("uses plain words", () => {
    expect(statusLabel("PendingCoordinatorApproval")).toBe("Waiting for your decision");
    expect(statusLabel("FailedSafe")).toBe("Stopped safely");
  });
  it("falls back to the raw value for unknown statuses", () => {
    expect(statusLabel("Something")).toBe("Something");
  });
  it("knows which workflows are over", () => {
    expect(isFinished("Executed")).toBe(true);
    expect(isFinished("Rejected")).toBe(true);
    expect(isFinished("FailedSafe")).toBe(true);
    expect(isFinished("Approved")).toBe(false);
    expect(isFinished("RevisionRequested")).toBe(false);
  });
});
