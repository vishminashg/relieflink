import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockFetch, staffSession } from "../test/fixtures";
import { renderApp } from "../test/render";

async function signIn(email = "c@x.lk", password = "Passw0rd!") {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Email"), email);
  await user.type(screen.getByLabelText("Password"), password);
  await user.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("Login", () => {
  it("sends signed-out visitors to the sign in page", () => {
    renderApp("/requests", false);
    expect(screen.getByRole("heading", { name: "Coordinator console" })).toBeInTheDocument();
  });

  it("signs a coordinator in and shows the overview", async () => {
    mockFetch({
      "POST /api/auth/login": staffSession,
      "GET /api/relief-requests/summary": { total: 3, open: 2, criticalOpen: 1, byStatus: {}, byUrgency: {}, byDistrict: {} },
      "GET /api/relief-requests": { items: [], page: 1, pageSize: 30, totalCount: 0, totalPages: 0 },
    });
    renderApp("/login", false);
    await signIn();
    expect(await screen.findByRole("heading", { name: "Overview" })).toBeInTheDocument();
    expect(JSON.parse(sessionStorage.getItem("relieflink.session")!).token).toBe("t");
  });

  it.each(["Citizen", "Volunteer"])("refuses a %s and keeps no token", async (role) => {
    mockFetch({ "POST /api/auth/login": { ...staffSession, user: { ...staffSession.user, role } } });
    renderApp("/login", false);
    await signIn();
    expect(await screen.findByRole("alert")).toHaveTextContent("This console is for coordinators and administrators");
    expect(sessionStorage.getItem("relieflink.session")).toBeNull();
  });

  it("shows the server's message for a wrong password", async () => {
    mockFetch({ "POST /api/auth/login": new Response(JSON.stringify({ title: "Email or password is wrong." }), { status: 401 }) });
    renderApp("/login", false);
    await signIn();
    expect(await screen.findByRole("alert")).toHaveTextContent("Email or password is wrong.");
  });
});
