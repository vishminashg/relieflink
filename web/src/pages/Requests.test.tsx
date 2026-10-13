import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { makeRequest, mockFetch, paged } from "../test/fixtures";
import { renderApp } from "../test/render";

describe("Requests queue", () => {
  it("lists requests with score, urgency, district and needs", async () => {
    mockFetch({ "GET /api/relief-requests": paged([makeRequest(), makeRequest({ id: "r2", district: "Galle", urgency: "Low", triageScore: 12 })]) });
    renderApp("/requests");
    const list = await screen.findByRole("list", { busy: false });
    const rows = within(list).getAllByRole("link");
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent("Gampaha");
    expect(rows[0]).toHaveTextContent("82");
    expect(rows[0]).toHaveTextContent("Water 20");
    expect(rows[0]).toHaveClass("row-critical");
    expect(rows[1]).toHaveClass("row-low");
  });

  it("asks for the most urgent first by default and sends filters when changed", async () => {
    const { fn } = mockFetch({ "GET /api/relief-requests": paged([makeRequest()]) });
    renderApp("/requests");
    await screen.findByText("1 request");
    expect(String(fn.mock.calls.at(-1)![0])).toContain("sort=urgency");

    const user = userEvent.setup();
    await user.selectOptions(screen.getByLabelText("Urgency"), "Critical");
    await user.selectOptions(screen.getByLabelText("District"), "Galle");
    await waitFor(() => {
      const last = String(fn.mock.calls.at(-1)![0]);
      expect(last).toContain("urgency=Critical");
      expect(last).toContain("district=Galle");
    });
  });

  it("searches only when the form is submitted and goes back to page 1", async () => {
    const { fn } = mockFetch({ "GET /api/relief-requests": paged([makeRequest()]) });
    renderApp("/requests");
    await screen.findByText("1 request");
    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Search address or notes"), "temple");
    expect(fn.mock.calls.some((c) => String(c[0]).includes("search="))).toBe(false);
    await user.click(screen.getByRole("button", { name: "Search" }));
    await waitFor(() => expect(String(fn.mock.calls.at(-1)![0])).toContain("search=temple"));
  });

  it("shows an empty state with advice", async () => {
    mockFetch({ "GET /api/relief-requests": paged([]) });
    renderApp("/requests");
    expect(await screen.findByText("No requests match these filters.")).toBeInTheDocument();
  });

  it("shows the error and lets the coordinator retry", async () => {
    let fail = true;
    mockFetch({
      "GET /api/relief-requests": () => (fail ? new Response(JSON.stringify({ title: "Database is down" }), { status: 500 }) : paged([makeRequest()])),
    });
    renderApp("/requests");
    expect(await screen.findByRole("alert")).toHaveTextContent("Database is down");
    fail = false;
    await userEvent.setup().click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText("1 request")).toBeInTheDocument();
  });
});
