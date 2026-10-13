import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { WorkflowStatus } from "../api/types";
import { makeWorkflow, mockFetch } from "../test/fixtures";
import { renderApp } from "../test/render";

const load = (status: WorkflowStatus, extra: Record<string, unknown> = {}) =>
  mockFetch({ "GET /api/workflows/w1": makeWorkflow(status), ...extra });

const button = (name: string) => screen.queryByRole("button", { name });

describe("Workflow detail: buttons follow the state", () => {
  it("shows approve, revise and reject while waiting for the coordinator", async () => {
    load("PendingCoordinatorApproval");
    renderApp("/workflows/w1");
    expect(await screen.findByRole("button", { name: "Approve plan" })).toBeEnabled();
    expect(button("Ask for a new draft")).toBeDisabled();
    expect(button("Reject plan")).toBeDisabled();
    expect(button("Execute plan")).not.toBeInTheDocument();
  });

  it("shows only execute once approved", async () => {
    load("Approved");
    renderApp("/workflows/w1");
    expect(await screen.findByRole("button", { name: "Execute plan" })).toBeEnabled();
    expect(button("Approve plan")).not.toBeInTheDocument();
    expect(button("Reject plan")).not.toBeInTheDocument();
  });

  it.each(["Planning", "Executed", "Rejected", "FailedSafe"] as const)("shows no decision buttons while %s", async (status) => {
    load(status);
    renderApp("/workflows/w1");
    await screen.findByRole("heading", { name: "Plan relief for Gampaha" });
    for (const name of ["Approve plan", "Ask for a new draft", "Reject plan", "Execute plan"]) expect(button(name)).not.toBeInTheDocument();
  });
});

describe("Workflow detail: actions", () => {
  it("approves without a comment", async () => {
    const { calls } = load("PendingCoordinatorApproval", { "POST /api/workflows/w1/approve": makeWorkflow("Approved") });
    renderApp("/workflows/w1");
    await userEvent.setup().click(await screen.findByRole("button", { name: "Approve plan" }));
    expect(await screen.findByRole("button", { name: "Execute plan" })).toBeInTheDocument();
    expect(calls.find((c) => c.key.endsWith("/approve"))!.body).toEqual({});
  });

  it("revises only with a comment of 3 or more characters", async () => {
    const { calls } = load("PendingCoordinatorApproval", { "POST /api/workflows/w1/revise": makeWorkflow("RevisionRequested") });
    renderApp("/workflows/w1");
    const user = userEvent.setup();
    const revise = await screen.findByRole("button", { name: "Ask for a new draft" });
    await user.type(screen.getByLabelText(/Comment/), "ab");
    expect(revise).toBeDisabled();
    await user.type(screen.getByLabelText(/Comment/), "c  ");
    expect(revise).toBeEnabled();
    await user.click(revise);
    expect(await screen.findByText(/the planner is working on a new plan/)).toBeInTheDocument();
    expect(calls.find((c) => c.key.endsWith("/revise"))!.body).toEqual({ comment: "abc" });
  });

  it("rejects the plan and can reject the request too", async () => {
    const { calls } = load("PendingCoordinatorApproval", { "POST /api/workflows/w1/reject": makeWorkflow("Rejected") });
    renderApp("/workflows/w1");
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText(/Comment/), "Unsafe route");
    await user.click(screen.getByLabelText(/also reject the relief request/));
    await user.click(screen.getByRole("button", { name: "Reject plan" }));
    expect(await screen.findByText("The plan was rejected.")).toBeInTheDocument();
    expect(calls.find((c) => c.key.endsWith("/reject"))!.body).toEqual({ comment: "Unsafe route", rejectRequest: true });
  });

  it("executes an approved plan", async () => {
    load("Approved", { "POST /api/workflows/w1/execute": makeWorkflow("Executed") });
    renderApp("/workflows/w1");
    await userEvent.setup().click(await screen.findByRole("button", { name: "Execute plan" }));
    await screen.findByRole("heading", { name: "The plan" });
    expect(button("Execute plan")).not.toBeInTheDocument();
  });

  it("shows a conflict message and reloads the real state", async () => {
    const { calls } = load("PendingCoordinatorApproval", {
      "POST /api/workflows/w1/approve": new Response(JSON.stringify({ title: "The workflow changed. Reload and try again." }), { status: 409 }),
    });
    renderApp("/workflows/w1");
    await userEvent.setup().click(await screen.findByRole("button", { name: "Approve plan" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("The workflow changed");
    expect(calls.filter((c) => c.key === "GET /api/workflows/w1").length).toBeGreaterThan(1);
  });
});

describe("Workflow detail: evidence", () => {
  it("shows the plan, safety checks grouped by draft, and the audit trail", async () => {
    load("PendingCoordinatorApproval");
    renderApp("/workflows/w1");
    expect(await screen.findByText("Deliver water and food within 6 hours.")).toBeInTheDocument();
    expect(screen.getByText("Draft 1: 1 check failed")).toBeInTheDocument();
    expect(screen.getByText("Draft 2: all checks passed")).toBeInTheDocument();
    expect(screen.getByText(/Water missing/)).toBeInTheDocument();
    expect(screen.getByText("get_request")).toBeInTheDocument();
    expect(screen.getByText("Road may be blocked")).toBeInTheDocument();
  });

  it("explains why a workflow stopped", async () => {
    mockFetch({ "GET /api/workflows/w1": makeWorkflow("FailedSafe", { workflow: { ...makeWorkflow("FailedSafe").workflow, failureReason: "The planner kept dropping a need." } }) });
    renderApp("/workflows/w1");
    expect(await screen.findByText(/The planner kept dropping a need\./)).toBeInTheDocument();
    expect(screen.getByText(/Nothing was sent to the field/)).toBeInTheDocument();
  });
});
