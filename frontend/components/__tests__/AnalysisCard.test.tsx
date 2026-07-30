import { render, screen, fireEvent } from "@testing-library/react";
import { AnalysisCard } from "@/components/AnalysisCard";
import { LanguageProvider } from "@/lib/language-context";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });

const ANALYSIS: AnalysisResult = {
  summary: LOC("Summary text"),
  documentType: LOC("Letter"),
  urgencyLevel: "Medium",
  keyPoints: [LOC("Point one")],
  requiredActions: [
    { description: LOC("Pay the fee"), isMandatory: true },
    { description: LOC("Consider appealing"), isMandatory: false },
  ],
  deadlines: [],
  explanation: LOC("Explanation text"),
};

function renderCard() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <AnalysisCard analysis={ANALYSIS} />
    </LanguageProvider>
  );
}

describe("AnalysisCard mandatory action chip", () => {
  it("shows a 'Required' text chip next to mandatory actions", () => {
    renderCard();
    expect(screen.getByText("Required")).toBeInTheDocument();
  });

  it("does not show the chip next to optional actions", () => {
    renderCard();
    const items = screen.getAllByRole("listitem");
    const optionalItem = items.find((li) => li.textContent?.includes("Consider appealing"))!;
    expect(optionalItem.textContent).not.toContain("Required");
  });

  it("hides the warning icon from assistive tech (the text chip carries the meaning now)", () => {
    const { container } = renderCard();
    const icon = container.querySelector("svg.text-orange-500");
    expect(icon).toHaveAttribute("aria-hidden", "true");
  });
});

describe("AnalysisCard checkbox persistence and calendar export", () => {
  beforeEach(() => window.localStorage.clear());

  it("persists a checked required action across remounts, keyed by documentId", () => {
    window.localStorage.setItem("lang", "en");
    const { unmount } = render(
      <LanguageProvider><AnalysisCard analysis={ANALYSIS} documentId="doc-1" /></LanguageProvider>
    );
    fireEvent.click(screen.getAllByRole("checkbox")[0]);
    unmount();

    render(<LanguageProvider><AnalysisCard analysis={ANALYSIS} documentId="doc-1" /></LanguageProvider>);
    expect(screen.getAllByRole("checkbox")[0]).toBeChecked();
  });

  it("renders an 'Add to calendar' link for each dated deadline", () => {
    window.localStorage.setItem("lang", "en");
    const withDeadline: AnalysisResult = {
      ...ANALYSIS,
      deadlines: [{ date: "2026-08-12T00:00:00Z", description: LOC("Payment due") }],
    };
    render(<LanguageProvider><AnalysisCard analysis={withDeadline} documentId="doc-1" /></LanguageProvider>);
    expect(screen.getByText("Add to calendar")).toBeInTheDocument();
  });
});
