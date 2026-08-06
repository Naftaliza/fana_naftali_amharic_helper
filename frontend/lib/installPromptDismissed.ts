// Same one-visit localStorage flag pattern as lib/onboarding.ts.

const KEY = "installPromptDismissed";

export function hasDismissedInstallPrompt(): boolean {
  if (typeof window === "undefined") return false;
  return window.localStorage.getItem(KEY) === "1";
}

export function markInstallPromptDismissed(): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(KEY, "1");
}
