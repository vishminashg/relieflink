import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { makeAssignment, makeVolunteer, mockFetch, paged } from "../test/fixtures";
import { renderApp } from "../test/render";

describe("VolunteerDetail screen", () => {
  it("renders profile details, skills table, availability slots, and assignment history", async () => {
    mockFetch({
      "GET /api/volunteers/v1": makeVolunteer(),
      "GET /api/assignments": paged([makeAssignment()]),
    });

    renderApp("/volunteers/v1");

    expect(await screen.findByRole("heading", { name: "Sunil Fernando" })).toBeInTheDocument();
    expect(screen.getByText("sunil@relief.lk")).toBeInTheDocument();
    expect(screen.getByText("Kamala Fernando (0779988776)")).toBeInTheDocument();
    expect(screen.getByText("30 hours / week")).toBeInTheDocument();

    // Skills
    expect(screen.getByText("First Aid")).toBeInTheDocument();
    expect(screen.getByText("SLRC-FA-2025")).toBeInTheDocument();
    expect(screen.getByText("Verified ✓")).toBeInTheDocument();

    // Availability
    expect(screen.getByText(/Every Monday:/)).toBeInTheDocument();
    expect(screen.getByText(/08:00 - 16:00/)).toBeInTheDocument();

    // Assignment History
    expect(await screen.findByText("First Aid & Medical Evacuation")).toBeInTheDocument();
    expect(screen.getByText("Assigned")).toBeInTheDocument();
  });

  it("submits profile edits to the API", async () => {
    const { calls } = mockFetch({
      "GET /api/volunteers/v1": makeVolunteer(),
      "GET /api/assignments": paged([]),
      "PUT /api/volunteers/v1": (body: unknown) => ({
        ...makeVolunteer(),
        emergencyContactName: (body as any).emergencyContactName,
      }),
    });

    renderApp("/volunteers/v1");
    await screen.findByRole("heading", { name: "Sunil Fernando" });

    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Edit Profile" }));

    const emergencyInput = screen.getByLabelText("Emergency Contact Name (ICE)");
    await user.clear(emergencyInput);
    await user.type(emergencyInput, "Nimal Fernando");

    await user.click(screen.getByRole("button", { name: "Save Profile" }));

    await waitFor(() => {
      const putCall = calls.find((c) => c.key === "PUT /api/volunteers/v1");
      expect(putCall).toBeDefined();
      expect((putCall!.body as any).emergencyContactName).toBe("Nimal Fernando");
    });
  });

  it("adds a new certified skill and sends update to API", async () => {
    const { calls } = mockFetch({
      "GET /api/volunteers/v1": makeVolunteer(),
      "GET /api/assignments": paged([]),
      "PUT /api/volunteers/v1/skills": (_body: unknown) => makeVolunteer(),
    });

    renderApp("/volunteers/v1");
    await screen.findByRole("heading", { name: "Sunil Fernando" });

    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "+ Add Skill" }));

    await user.selectOptions(screen.getByLabelText("Skill"), "BoatOperation");
    await user.selectOptions(screen.getByLabelText("Proficiency"), "Expert");
    await user.type(screen.getByLabelText("Certificate / License Reference (Optional)"), "SL-NAVY-B2");

    await user.click(screen.getByRole("button", { name: "Add Certified Skill" }));

    await waitFor(() => {
      const putCall = calls.find((c) => c.key === "PUT /api/volunteers/v1/skills");
      expect(putCall).toBeDefined();
      const skills = (putCall!.body as any).skills;
      expect(skills.some((s: any) => s.skill === "BoatOperation" && s.proficiency === "Expert")).toBe(true);
    });
  });

  it("updates mission status to InProgress", async () => {
    const { calls } = mockFetch({
      "GET /api/volunteers/v1": makeVolunteer({ activeAssignmentsCount: 1 }),
      "GET /api/assignments": paged([makeAssignment({ status: "Assigned" })]),
      "PATCH /api/assignments/asg-1/status": (_body: unknown) =>
        makeAssignment({ status: "InProgress" }),
    });

    renderApp("/volunteers/v1");
    const startBtn = await screen.findByRole("button", { name: "Start Mission" });

    await userEvent.setup().click(startBtn);

    await waitFor(() => {
      const patchCall = calls.find((c) => c.key === "PATCH /api/assignments/asg-1/status");
      expect(patchCall).toBeDefined();
      expect((patchCall!.body as any).status).toBe("InProgress");
    });
  });

  it("cancels assignment with rationale", async () => {
    const { calls } = mockFetch({
      "GET /api/volunteers/v1": makeVolunteer({ activeAssignmentsCount: 1 }),
      "GET /api/assignments": paged([makeAssignment({ status: "Assigned" })]),
      "POST /api/assignments/asg-1/cancel": (_body: unknown) =>
        makeAssignment({ status: "Cancelled" }),
    });

    renderApp("/volunteers/v1");
    const cancelBtn = await screen.findByRole("button", { name: "Cancel" });

    const user = userEvent.setup();
    await user.click(cancelBtn);

    // Modal opens
    expect(screen.getByRole("heading", { name: "Cancel Volunteer Mission" })).toBeInTheDocument();
    await user.type(screen.getByLabelText("Cancellation Reason"), "Road blocked by landslide");
    await user.click(screen.getByRole("button", { name: "Confirm Cancellation" }));

    await waitFor(() => {
      const cancelCall = calls.find((c) => c.key === "POST /api/assignments/asg-1/cancel");
      expect(cancelCall).toBeDefined();
      expect((cancelCall!.body as any).reason).toBe("Road blocked by landslide");
    });
  });
});
