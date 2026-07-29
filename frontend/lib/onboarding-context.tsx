"use client";

import { createContext, useCallback, useContext, useState, type ReactNode } from "react";

/**
 * Whether the onboarding walkthrough dialog is open, and a way to (re-)open it from anywhere —
 * the Help page, a nav link, etc. — not just the one-time first-visit auto-show. The dialog
 * itself (components/Onboarding.tsx) is mounted once in the root layout and reads this state.
 */
type OnboardingContextValue = {
  open: boolean;
  show: () => void;
  hide: () => void;
};

const OnboardingContext = createContext<OnboardingContextValue | null>(null);

export function OnboardingProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const show = useCallback(() => setOpen(true), []);
  const hide = useCallback(() => setOpen(false), []);
  return <OnboardingContext.Provider value={{ open, show, hide }}>{children}</OnboardingContext.Provider>;
}

export function useOnboarding() {
  const ctx = useContext(OnboardingContext);
  if (!ctx) throw new Error("useOnboarding must be used within OnboardingProvider");
  return ctx;
}
