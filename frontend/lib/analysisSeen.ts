// Tracks whether the visitor has ever seen a completed analysis, mirroring the localStorage
// approach in lib/onboarding.ts. Used to gate the install prompt: only offer "Add to Home
// Screen" once the product's value has actually landed, not on cold load.

const KEY = "hasSeenAnalysis";

export function hasSeenAnalysis(): boolean {
  if (typeof window === "undefined") return false;
  return window.localStorage.getItem(KEY) === "1";
}

export function markAnalysisSeen(): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(KEY, "1");
}
