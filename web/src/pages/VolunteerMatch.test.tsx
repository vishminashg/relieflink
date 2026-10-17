import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { makeAssignment, makeMatchResult, mockFetch } from "../test/fixtures";
import { renderApp } from "../test/render";

describe("VolunteerMatch screen", () => {
  it("renders matching criteria form", () => {
    mockFetch({});
    renderApp("/volunteers/match");

    expect(screen.getByRole("heading", { name: "Volunteer Match & Dispatch" })).toBeInTheDocument();
    expect(screen.getByLabelText("Target Disaster District")).toBeInTheDocument();
    expect(screen.getByLabelText("Mission Start Time")).toBeInTheDocument();
    expect(screen.getByLabelText("Mission End Time")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Find Matching Volunteers" })).toBeInTheDocument();
  });

  it("executes match algorithm and displays ranked candidates", async () => {
    const candidate1 = makeMatchResult({ score: 95, quality: "Excellent" });
    const candidate2 = makeMatchResult({
      volunteer: {
        ...candidate1.volunteer,
        id: "v2",
        district: "Gampaha",
        user: { id: "u3", fullName: "Nimal Silva", email: "nimal@relief.lk", phone: "0778899001" },
      },
      score: 75,
      quality: "Good",
    });

    const { fn } = mockFetch({
      "GET /api/volunteers/match": [candidate1, candidate2],
    });

    renderApp("/volunteers/match");

    const user = userEvent.setup();
    await user.selectOptions(screen.getByLabelText("Target Disaster District"), "Colombo");
    await user.click(screen.getByRole("button", { name: /First Aid/ })); // Toggle First Aid skill
    await user.click(screen.getByRole("button", { name: "Find Matching Volunteers" }));

    expect(await screen.findByRole("heading", { name: /Ranked Match Results/ })).toBeInTheDocument();
    expect(screen.getByText("Sunil Fernando")).toBeInTheDocument();
    expect(screen.getByText("Excellent (95 / 100)")).toBeInTheDocument();
    expect(screen.getByText("Nimal Silva")).toBeInTheDocument();
    expect(screen.getByText("Good (75 / 100)")).toBeInTheDocument();

    const lastCall = String(fn.mock.calls.at(-1)![0]);
    expect(lastCall).toContain("district=Colombo");
    expect(lastCall).toContain("skills=FirstAid");
  });

  it("opens dispatch modal and successfully dispatches a volunteer", async () => {
    const candidate = makeMatchResult({ score: 90, quality: "Excellent" });

    const { calls } = mockFetch({
      "GET /api/volunteers/match": [candidate],
      "POST /api/assignments": makeAssignment(),
    });

    renderApp("/volunteers/match");
    await userEvent.setup().click(screen.getByRole("button", { name: "Find Matching Volunteers" }));

    const dispatchBtn = await screen.findByRole("button", { name: "Dispatch Volunteer" });
    await userEvent.setup().click(dispatchBtn);

    // Modal opens
    expect(screen.getByRole("heading", { name: "Confirm Mission Dispatch" })).toBeInTheDocument();
    const taskInput = screen.getByLabelText("Mission Role / Task Description");
    expect(taskInput).toHaveValue("First Aid Field Response");

    await userEvent.setup().click(screen.getByRole("button", { name: "Confirm & Dispatch" }));

    await waitFor(() => {
      const postCall = calls.find((c) => c.key === "POST /api/assignments");
      expect(postCall).toBeDefined();
      expect((postCall!.body as any).volunteerId).toBe("v1");
      expect((postCall!.body as any).roleOrTask).toBe("First Aid Field Response");
    });

    expect(await screen.findByText(/Successfully dispatched Sunil Fernando/)).toBeInTheDocument();
  });

  it("handles double-booking schedule conflict error (HTTP 409)", async () => {
    const candidate = makeMatchResult({ score: 90, quality: "Excellent" });

    mockFetch({
      "GET /api/volunteers/match": [candidate],
      "POST /api/assignments": () =>
        new Response(
          JSON.stringify({
            title: "Schedule conflict detected.",
            detail: "Volunteer has an existing active assignment overlapping this time window (+ 30m transit buffer). Double-booking is strictly prohibited.",
          }),
          { status: 409, headers: { "Content-Type": "application/json" } }
        ),
    });

    renderApp("/volunteers/match");
    await userEvent.setup().click(screen.getByRole("button", { name: "Find Matching Volunteers" }));

    const dispatchBtn = await screen.findByRole("button", { name: "Dispatch Volunteer" });
    await userEvent.setup().click(dispatchBtn);

    await userEvent.setup().click(screen.getByRole("button", { name: "Confirm & Dispatch" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Schedule conflict detected.");
    expect(screen.getByText(/Double-booking is strictly prohibited/)).toBeInTheDocument();
  });
});
