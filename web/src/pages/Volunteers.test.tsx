import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { makeVolunteer, makeVolunteerSummary, mockFetch, paged } from "../test/fixtures";
import { renderApp } from "../test/render";

describe("Volunteers directory", () => {
  it("lists volunteers with district, certified skills, and active assignment count", async () => {
    mockFetch({
      "GET /api/volunteers": paged([
        makeVolunteer(),
        makeVolunteer({
          id: "v2",
          district: "Kandy",
          isActive: false,
          user: { id: "u3", fullName: "Anura Bandara", email: "anura@relief.lk", phone: "0773344556" },
          skills: [{ id: "s3", skill: "Medical", proficiency: "Expert", certificateReference: "SLMC-492", isVerified: true }],
          activeAssignmentsCount: 2,
        }),
      ]),
      "GET /api/volunteers/summary": makeVolunteerSummary(),
    });

    renderApp("/volunteers");

    // Header and summary figures
    expect(await screen.findByRole("heading", { name: "Volunteers" })).toBeInTheDocument();
    expect(await screen.findByText("Total registered")).toBeInTheDocument();
    expect(screen.getByText("45")).toBeInTheDocument(); // total
    expect(screen.getByText("38")).toBeInTheDocument(); // active

    const list = await screen.findByRole("list", { busy: false });
    const rows = within(list).getAllByRole("link");
    expect(rows).toHaveLength(2);

    expect(rows[0]).toHaveTextContent("Sunil Fernando • Colombo");
    expect(rows[0]).toHaveTextContent("First Aid (Advanced) ✓");
    expect(rows[0]).toHaveTextContent("Search & Rescue (Intermediate)");
    expect(rows[0]).toHaveTextContent("Available now");

    expect(rows[1]).toHaveTextContent("Anura Bandara • Kandy");
    expect(rows[1]).toHaveTextContent("Medical Response (Expert) ✓");
    expect(rows[1]).toHaveTextContent("2 active assignments");
    expect(rows[1]).toHaveTextContent("Inactive");
  });

  it("applies district, skill, and status filters", async () => {
    const { fn } = mockFetch({
      "GET /api/volunteers": paged([makeVolunteer()]),
      "GET /api/volunteers/summary": makeVolunteerSummary(),
    });

    renderApp("/volunteers");
    await screen.findByText("1 volunteer");

    const user = userEvent.setup();
    await user.selectOptions(screen.getByLabelText("District"), "Colombo");
    await user.selectOptions(screen.getByLabelText("Skill"), "FirstAid");
    await user.selectOptions(screen.getByLabelText("Status"), "Active only");

    await waitFor(() => {
      const last = String(fn.mock.calls.at(-1)![0]);
      expect(last).toContain("district=Colombo");
      expect(last).toContain("skill=FirstAid");
      expect(last).toContain("isActive=true");
    });
  });

  it("searches by keyword on form submission", async () => {
    const { fn } = mockFetch({
      "GET /api/volunteers": paged([makeVolunteer()]),
      "GET /api/volunteers/summary": makeVolunteerSummary(),
    });

    renderApp("/volunteers");
    await screen.findByText("1 volunteer");

    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Search name, email, phone"), "Sunil");
    expect(fn.mock.calls.some((c) => String(c[0]).includes("search="))).toBe(false);

    await user.click(screen.getByRole("button", { name: "Search" }));
    await waitFor(() => {
      expect(String(fn.mock.calls.at(-1)![0])).toContain("search=Sunil");
    });
  });

  it("renders empty state when no volunteers match criteria", async () => {
    mockFetch({
      "GET /api/volunteers": paged([]),
      "GET /api/volunteers/summary": makeVolunteerSummary(),
    });

    renderApp("/volunteers");
    expect(await screen.findByText("No volunteers match these filters.")).toBeInTheDocument();
  });

  it("displays server error and enables coordinator retry", async () => {
    let shouldFail = true;
    mockFetch({
      "GET /api/volunteers": () =>
        shouldFail
          ? new Response(JSON.stringify({ title: "Database timeout" }), { status: 500 })
          : paged([makeVolunteer()]),
      "GET /api/volunteers/summary": makeVolunteerSummary(),
    });

    renderApp("/volunteers");
    expect(await screen.findByRole("alert")).toHaveTextContent("Database timeout");

    shouldFail = false;
    await userEvent.setup().click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText("1 volunteer")).toBeInTheDocument();
  });
});
