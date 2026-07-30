// Stashes the anonymous trial analysis just before navigating to /register, so the single
// highest-intent moment in the funnel (tapping "register" right after reading a good analysis)
// doesn't destroy it. Same typed-blob localStorage pattern as lib/leadFeedback.ts.

import type { AnalysisResult } from "@/lib/types";

const KEY = "pendingTrialAnalysis";

export interface PendingTrialAnalysis {
  analysis: AnalysisResult;
  stashedAt: number;
}

export function setPendingTrialAnalysis(analysis: AnalysisResult): void {
  window.localStorage.setItem(KEY, JSON.stringify({ analysis, stashedAt: Date.now() }));
}

export function getPendingTrialAnalysis(): PendingTrialAnalysis | null {
  if (typeof window === "undefined") return null;
  try {
    return JSON.parse(window.localStorage.getItem(KEY) ?? "null");
  } catch {
    return null;
  }
}

export function clearPendingTrialAnalysis(): void {
  window.localStorage.removeItem(KEY);
}
