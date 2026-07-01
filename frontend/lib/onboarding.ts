// Tracks whether the first-run onboarding walkthrough has been shown, mirroring the
// simple localStorage approach used by lib/trial.ts.

const KEY = "onboardingSeen";

export function hasSeenOnboarding(): boolean {
  if (typeof window === "undefined") return true;
  return window.localStorage.getItem(KEY) === "1";
}

export function markOnboardingSeen(): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(KEY, "1");
}
