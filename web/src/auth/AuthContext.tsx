import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, ApiError, setToken, setUnauthorizedHandler } from "../api/client";
import type { User } from "../api/types";

const KEY = "relieflink.session";

interface Session { token: string; user: User; expiresAtUtc: string }

interface AuthValue {
  user: User | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

export const isStaff = (user: User | null) => user?.role === "Coordinator" || user?.role === "Admin";

function restore(): Session | null {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    if (new Date(session.expiresAtUtc).getTime() <= Date.now()) return null;
    setToken(session.token);
    return session;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(restore);

  const logout = useCallback(() => {
    setToken(null);
    sessionStorage.removeItem(KEY);
    setSession(null);
  }, []);

  useEffect(() => { setUnauthorizedHandler(logout); }, [logout]);

  const login = useCallback(async (email: string, password: string) => {
    const result = await api.login(email, password);
    if (result.user.role !== "Coordinator" && result.user.role !== "Admin") {
      // Never keep a token for someone this console is not meant for.
      throw new ApiError(403, "This console is for coordinators and administrators. Citizens and volunteers use the mobile app.");
    }
    setToken(result.token);
    const next = { token: result.token, user: result.user, expiresAtUtc: result.expiresAtUtc };
    sessionStorage.setItem(KEY, JSON.stringify(next));
    setSession(next);
  }, []);

  const value = useMemo(() => ({ user: session?.user ?? null, login, logout }), [session, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used inside AuthProvider.");
  return value;
}
