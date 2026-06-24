// Client-side free-trial counter. Anonymous users get TRIAL_LIMIT free analyses before
// being asked to sign up. Stored in localStorage — intentionally simple (the goal is to
// let people try the product, not strict metering).

export const TRIAL_LIMIT = 3;
const KEY = "trialCount";

export function getTrialCount(): number {
  if (typeof window === "undefined") return 0;
  return Number(window.localStorage.getItem(KEY) ?? "0");
}

export function trialRemaining(): number {
  return Math.max(0, TRIAL_LIMIT - getTrialCount());
}

export function incrementTrial(): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(KEY, String(getTrialCount() + 1));
}
