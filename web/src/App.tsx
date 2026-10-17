import { Navigate, Route, Routes } from "react-router-dom";
import { isStaff, useAuth } from "./auth/AuthContext";
import { Layout } from "./components/Layout";
import { Dashboard } from "./pages/Dashboard";
import { Login } from "./pages/Login";
import { RequestDetail } from "./pages/RequestDetail";
import { Requests } from "./pages/Requests";
import { VolunteerDetail } from "./pages/VolunteerDetail";
import { VolunteerMatch } from "./pages/VolunteerMatch";
import { Volunteers } from "./pages/Volunteers";
import { WorkflowDetail } from "./pages/WorkflowDetail";
import { Workflows } from "./pages/Workflows";

function RequireStaff() {
  const { user } = useAuth();
  if (!user || !isStaff(user)) return <Navigate to="/login" replace />;
  return <Layout />;
}

export function App() {
  return (
    <>
      <a className="skip-link" href="#main">Skip to content</a>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route element={<RequireStaff />}>
          <Route index element={<Dashboard />} />
          <Route path="requests" element={<Requests />} />
          <Route path="requests/:id" element={<RequestDetail />} />
          <Route path="volunteers" element={<Volunteers />} />
          <Route path="volunteers/:id" element={<VolunteerDetail />} />
          <Route path="volunteers/match" element={<VolunteerMatch />} />
          <Route path="workflows" element={<Workflows />} />
          <Route path="workflows/:id" element={<WorkflowDetail />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}
