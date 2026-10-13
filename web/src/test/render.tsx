import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { App } from "../App";
import { AuthProvider } from "../auth/AuthContext";
import { staffSession } from "./fixtures";

/** Renders the whole app at a route, optionally already signed in as staff. */
export function renderApp(route = "/", signedIn = true) {
  if (signedIn) sessionStorage.setItem("relieflink.session", JSON.stringify(staffSession));
  return render(
    <MemoryRouter initialEntries={[route]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <AuthProvider><App /></AuthProvider>
    </MemoryRouter>,
  );
}
