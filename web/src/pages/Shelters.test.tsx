import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockFetch } from "../test/fixtures";
import { renderApp } from "../test/render";

const shelter = {
  id: "s1", name: "Kalutara Hall", district: "Kalutara", address: "Main Road",
  latitude: 6.58, longitude: 79.96, capacity: 100, occupied: 40, available: 60,
  isActive: true, notes: null, createdAt: "2026-10-17T00:00:00Z", updatedAt: "2026-10-17T00:00:00Z",
};

describe("Shelters", () => {
  it("lists capacity and deactivates a shelter after confirmation", async () => {
    const { fn } = mockFetch({
      "GET /api/shelters": { items: [shelter], total: 1, page: 1, pageSize: 100 },
      "DELETE /api/shelters/s1": new Response(null, { status: 204 }),
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    renderApp("/shelters");

    expect(await screen.findByText("Kalutara Hall")).toBeInTheDocument();
    expect(screen.getByText("60")).toBeInTheDocument();
    await userEvent.setup().click(screen.getByRole("button", { name: "Deactivate" }));
    await waitFor(() => expect(fn.mock.calls.some(([url]) => String(url).endsWith("/api/shelters/s1"))).toBe(true));
  });
});
