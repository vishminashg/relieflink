import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { makeWorkflow, mockFetch, paged } from "../test/fixtures";
import { renderApp } from "../test/render";

describe("AI plans list", () => {
  it("starts on plans waiting for a decision", async () => {
    const { fn } = mockFetch({ "GET /api/workflows": paged([makeWorkflow("PendingCoordinatorApproval").workflow]) });
    renderApp("/workflows");
    expect(await screen.findByText("Plan relief for Gampaha")).toBeInTheDocument();
    expect(String(fn.mock.calls[0][0])).toContain("status=PendingCoordinatorApproval");
  });

  it("can show every plan", async () => {
    const { fn } = mockFetch({ "GET /api/workflows": paged([]) });
    renderApp("/workflows");
    await screen.findByText("No plans here.");
    await userEvent.setup().selectOptions(screen.getByLabelText("Show"), "All plans");
    await waitFor(() => expect(String(fn.mock.calls.at(-1)![0])).not.toContain("status="));
  });
});

describe("Navigation", () => {
  it("signs out and returns to the sign in page", async () => {
    mockFetch({ "GET /api/workflows": paged([]) });
    renderApp("/workflows");
    await userEvent.setup().click(await screen.findByRole("button", { name: "Sign out" }));
    expect(await screen.findByRole("heading", { name: "Coordinator console" })).toBeInTheDocument();
    expect(sessionStorage.getItem("relieflink.session")).toBeNull();
  });
});
