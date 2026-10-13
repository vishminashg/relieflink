import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { makeRequest, makeWorkflow, mockFetch, paged } from "../test/fixtures";
import { renderApp } from "../test/render";

const noPlans = { "GET /api/workflows": paged([]) };

describe("Request detail", () => {
  it("shows needs, the reasons behind the score and the household", async () => {
    mockFetch({ "GET /api/relief-requests/r1": makeRequest(), ...noPlans });
    renderApp("/requests/r1");
    expect(await screen.findByRole("heading", { name: "Gampaha" })).toBeInTheDocument();
    expect(screen.getByText("Home is flooded")).toBeInTheDocument();
    expect(screen.getByText(/Why this score: 82/)).toBeInTheDocument();
    expect(screen.getByText("Nimali Perera, 0771234567")).toBeInTheDocument();
  });

  it("keeps the override button disabled until the reason has 3 characters", async () => {
    mockFetch({ "GET /api/relief-requests/r1": makeRequest(), ...noPlans });
    renderApp("/requests/r1");
    const save = await screen.findByRole("button", { name: "Save urgency" });
    expect(save).toBeDisabled();
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/Reason for the change/), "ab");
    expect(save).toBeDisabled();
    await user.type(screen.getByLabelText(/Reason for the change/), "c");
    expect(save).toBeEnabled();
  });

  it("sends the override and shows the updated urgency", async () => {
    const { calls } = mockFetch({
      "GET /api/relief-requests/r1": makeRequest(),
      "POST /api/relief-requests/r1/triage": makeRequest({ urgency: "Medium", urgencyOverridden: true, urgencyOverrideReason: "Checked by phone" }),
      ...noPlans,
    });
    renderApp("/requests/r1");
    const user = userEvent.setup();
    await screen.findByRole("button", { name: "Save urgency" });
    await user.selectOptions(screen.getByLabelText("New urgency"), "Medium");
    await user.type(screen.getByLabelText(/Reason for the change/), "Checked by phone");
    await user.click(screen.getByRole("button", { name: "Save urgency" }));
    expect(await screen.findByText(/Urgency was set by hand to Medium/)).toBeInTheDocument();
    expect(calls.find((c) => c.key === "POST /api/relief-requests/r1/triage")!.body).toEqual({ urgency: "Medium", reason: "Checked by phone" });
    expect(screen.getByRole("button", { name: "Go back to the automatic score" })).toBeInTheDocument();
  });

  it("offers an AI plan only for a submitted request and opens the new workflow", async () => {
    mockFetch({
      "GET /api/relief-requests/r1": makeRequest(),
      "POST /api/workflows": makeWorkflow("PendingCoordinatorApproval"),
      "GET /api/workflows/w1": makeWorkflow("PendingCoordinatorApproval"),
      ...noPlans,
    });
    renderApp("/requests/r1");
    await userEvent.setup().click(await screen.findByRole("button", { name: "Draft a plan with AI" }));
    expect(await screen.findByRole("heading", { name: "Plan relief for Gampaha" })).toBeInTheDocument();
  });

  it("hides the AI button when the request is already under review", async () => {
    mockFetch({ "GET /api/relief-requests/r1": makeRequest({ status: "UnderReview" }), ...noPlans });
    renderApp("/requests/r1");
    await screen.findByRole("heading", { name: "Gampaha" });
    expect(screen.queryByRole("button", { name: "Draft a plan with AI" })).not.toBeInTheDocument();
  });

  it("hides editing controls once the request is closed", async () => {
    mockFetch({ "GET /api/relief-requests/r1": makeRequest({ status: "Cancelled", cancellationReason: "Duplicate" }), ...noPlans });
    renderApp("/requests/r1");
    await screen.findByRole("heading", { name: "Gampaha" });
    expect(screen.queryByRole("button", { name: "Save urgency" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel request" })).not.toBeInTheDocument();
    expect(screen.getByText(/Cancelled: Duplicate/)).toBeInTheDocument();
  });

  it("cancels with a reason", async () => {
    const { calls } = mockFetch({
      "GET /api/relief-requests/r1": makeRequest(),
      "POST /api/relief-requests/r1/cancel": makeRequest({ status: "Cancelled", cancellationReason: "Duplicate request" }),
      ...noPlans,
    });
    renderApp("/requests/r1");
    const user = userEvent.setup();
    const cancel = await screen.findByRole("button", { name: "Cancel request" });
    expect(cancel).toBeDisabled();
    await user.type(screen.getByLabelText(/Reason for cancelling/), "Duplicate request");
    await user.click(cancel);
    await waitFor(() => expect(screen.getByText(/Cancelled: Duplicate request/)).toBeInTheDocument());
    expect(calls.find((c) => c.key.endsWith("/cancel"))!.body).toEqual({ reason: "Duplicate request" });
  });

  it("shows the server's message when an action is refused", async () => {
    mockFetch({
      "GET /api/relief-requests/r1": makeRequest(),
      "POST /api/workflows": new Response(JSON.stringify({ title: "A workflow is already running for this request." }), { status: 409 }),
      ...noPlans,
    });
    renderApp("/requests/r1");
    await userEvent.setup().click(await screen.findByRole("button", { name: "Draft a plan with AI" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("A workflow is already running");
  });
});
