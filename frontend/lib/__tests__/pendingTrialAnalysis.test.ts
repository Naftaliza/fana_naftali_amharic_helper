import { getPendingTrialAnalysis, setPendingTrialAnalysis, clearPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });
const ANALYSIS: AnalysisResult = {
  summary: LOC("s"), documentType: LOC("t"), urgencyLevel: "Low",
  keyPoints: [], requiredActions: [], deadlines: [], explanation: LOC("e"),
};

describe("pendingTrialAnalysis", () => {
  beforeEach(() => window.localStorage.clear());

  it("returns null when nothing has been stashed", () => {
    expect(getPendingTrialAnalysis()).toBeNull();
  });

  it("round-trips a stashed analysis", () => {
    setPendingTrialAnalysis(ANALYSIS);
    const pending = getPendingTrialAnalysis();
    expect(pending?.analysis).toEqual(ANALYSIS);
    expect(typeof pending?.stashedAt).toBe("number");
  });

  it("clears the stash", () => {
    setPendingTrialAnalysis(ANALYSIS);
    clearPendingTrialAnalysis();
    expect(getPendingTrialAnalysis()).toBeNull();
  });

  it("tolerates corrupted storage instead of throwing", () => {
    window.localStorage.setItem("pendingTrialAnalysis", "{not json");
    expect(getPendingTrialAnalysis()).toBeNull();
  });
});
