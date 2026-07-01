// Tracks the most recent provider contact so LeadFeedbackPrompt can later ask "did they help?".
// Same localStorage-flag approach as lib/trial.ts / lib/onboarding.ts.

const KEY = "pendingLeadFeedback";

export interface PendingFeedback {
  ref: string;
  providerName: string;
  contactedAt: number;
}

export function setPendingFeedback(f: PendingFeedback): void {
  window.localStorage.setItem(KEY, JSON.stringify(f));
}

export function getPendingFeedback(): PendingFeedback | null {
  if (typeof window === "undefined") return null;
  try {
    return JSON.parse(window.localStorage.getItem(KEY) ?? "null");
  } catch {
    return null;
  }
}

export function clearPendingFeedback(): void {
  window.localStorage.removeItem(KEY);
}
