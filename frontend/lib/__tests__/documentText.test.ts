import { buildPlainTextSummary } from "@/lib/documentText";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });

const ANALYSIS: AnalysisResult = {
  summary: LOC("This is a tax notice."),
  documentType: LOC("Tax notice"),
  urgencyLevel: "High",
  keyPoints: [LOC("Payment is overdue"), LOC("A penalty may apply")],
  requiredActions: [
    { description: LOC("Pay the balance"), isMandatory: true },
    { description: LOC("Consider requesting an extension"), isMandatory: false },
  ],
  deadlines: [{ date: "2026-08-12T00:00:00Z", description: LOC("Payment due") }],
  explanation: LOC("The tax authority says you owe money from last year."),
};

const t = (key: string) => ({ "doc.required": "Required", "doc.urgency": "Urgency", "doc.type": "Type" } as Record<string, string>)[key] ?? key;

describe("buildPlainTextSummary", () => {
  it("includes the document type and urgency", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("Tax notice");
    expect(text).toContain("High");
  });

  it("includes the summary and explanation", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("This is a tax notice.");
    expect(text).toContain("The tax authority says you owe money from last year.");
  });

  it("includes every required action, marking mandatory ones", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("Pay the balance");
    expect(text).toContain("Consider requesting an extension");
    expect(text.indexOf("Required")).toBeGreaterThanOrEqual(0);
  });

  it("includes deadlines with a formatted date", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("Payment due");
    expect(text).toMatch(/2026|Aug/);
  });

  it("omits the deadlines section entirely when there are none", () => {
    const text = buildPlainTextSummary({ ...ANALYSIS, deadlines: [] }, "en", t);
    expect(text).not.toMatch(/deadline/i);
  });
});
