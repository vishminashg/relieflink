import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export function Layout() {
  const { user, logout } = useAuth();
  return (
    <div className="shell">
      <aside className="rail">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">+</span>
          <span className="brand-name">ReliefLink</span>
        </div>
        <nav className="nav" aria-label="Main">
          <NavLink to="/" end>Overview</NavLink>
          <NavLink to="/requests">Requests</NavLink>
          <NavLink to="/workflows">AI plans</NavLink>
        </nav>
        <div className="rail-user">
          <p className="rail-name">{user?.fullName}</p>
          <p className="rail-role">{user?.role}</p>
          <button className="btn btn-rail" onClick={logout}>Sign out</button>
        </div>
      </aside>
      <main className="content" id="main">
        <Outlet />
      </main>
    </div>
  );
}
