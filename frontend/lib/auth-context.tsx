"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, tokenStore } from "@/lib/api";
import type { AuthUser } from "@/lib/types";

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  login: (email: string, password: string, remember: boolean) => Promise<void>;
  register: (
    email: string, password: string, displayName: string, preferredLanguage: number, organizationSlug?: string | null
  ) => Promise<{ email: string }>;
  verifyEmail: (email: string, token: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!tokenStore.access) {
      setLoading(false);
      return;
    }
    api.me().then(setUser).catch(() => tokenStore.clear()).finally(() => setLoading(false));
  }, []);

  const login = async (email: string, password: string, remember: boolean) => {
    const res = await api.login({ email, password });
    tokenStore.set(res.accessToken, res.refreshToken, remember);
    // Fetch /me so flags computed server-side (e.g. isAdmin) are authoritative.
    setUser(await api.me().catch(() => res.user));
  };

  const register = async (
    email: string, password: string, displayName: string, preferredLanguage: number, organizationSlug?: string | null
  ) => {
    // The account is created unverified — no tokens come back yet. The caller sends the
    // user to a "check your email" screen; login only happens once the emailed link is
    // clicked (see verifyEmail below).
    const res = await api.register({ email, password, displayName, preferredLanguage, organizationSlug });
    return { email: res.email };
  };

  const verifyEmail = async (email: string, token: string) => {
    const res = await api.verifyEmail(email, token);
    // No remember-me control on this flow — matches register's old always-persist behavior.
    tokenStore.set(res.accessToken, res.refreshToken, true);
    setUser(await api.me().catch(() => res.user));
  };

  const logout = () => {
    tokenStore.clear();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, register, verifyEmail, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
