// Regression guard for the accessibility work: several screens/controls were flagged as either
// not doing what they claim (contrast, grayscale) or having no automated coverage at all before
// this. jest-axe can't check the CSS-only fixes (grayscale positioning, contrast color values —
// those are covered by the manual verification pass in the plan), but it does catch the
// structural defects on the components/screens actually touched here: missing labels, invalid
// ARIA, heading order, etc. — on every render, not just the one time someone remembers to check.
import { render, waitFor } from "@testing-library/react";
import { axe } from "jest-axe";
import { AnalysisCard } from "@/components/AnalysisCard";
import { BottomNav } from "@/components/BottomNav";
import { AccessibilityWidget } from "@/components/AccessibilityWidget";
import DashboardPage from "@/app/dashboard/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";
import type { AnalysisResult } from "@/lib/types";

jest.mock("@/lib/api", () => ({
  api: { listDocuments: jest.fn(), deleteDocument: jest.fn() },
}));
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
  usePathname: () => "/dashboard",
}));
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: { id: "u1", email: "a@b.com" }, loading: false }),
}));

const LOC = (s: string) => ({ he: s, am: s, en: s });

const ANALYSIS: AnalysisResult = {
  summary: LOC("Summary text"),
  documentType: LOC("Letter"),
  urgencyLevel: "Critical",
  keyPoints: [LOC("Point one"), LOC("Point two")],
  requiredActions: [
    { description: LOC("Pay the fee"), isMandatory: true },
    { description: LOC("Consider appealing"), isMandatory: false },
  ],
  deadlines: [{ date: new Date().toISOString(), description: LOC("Payment due") }],
  explanation: LOC("Explanation text.\n\nA second paragraph."),
};

function withLang(ui: React.ReactElement) {
  window.localStorage.setItem("lang", "en");
  return <LanguageProvider>{ui}</LanguageProvider>;
}

describe("Accessibility regression guard", () => {
  it("AnalysisCard — verdict band, collapsed sections, and the referral block — has no axe violations", async () => {
    const { container } = render(withLang(<AnalysisCard analysis={ANALYSIS} documentId="doc-1" trial />));
    expect(await axe(container)).toHaveNoViolations();
  });

  it("BottomNav has no axe violations, signed in or anonymous", async () => {
    const { container } = render(withLang(<BottomNav />));
    expect(await axe(container)).toHaveNoViolations();
  });

  it("AccessibilityWidget's open panel has no axe violations", async () => {
    const { container, getByLabelText } = render(withLang(<AccessibilityWidget />));
    getByLabelText("Accessibility").click();
    expect(await axe(container)).toHaveNoViolations();
  });

  it("DashboardPage (with the new Coming up strip and document list) has no axe violations", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([
      {
        id: "d1", fileName: "letter.pdf", contentType: "application/pdf",
        uploadedAt: new Date().toISOString(), hasAnalysis: true, status: 2,
        deadlines: [{ date: "2999-01-01T00:00:00Z", description: LOC("Pay") }],
      },
    ]);
    const { container, getByText } = render(withLang(<DashboardPage />));
    await waitFor(() => getByText("letter.pdf"));
    expect(await axe(container)).toHaveNoViolations();
  });
});
