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

function renderCard(analysis: AnalysisResult = ANALYSIS, documentId?: string) {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <AnalysisCard analysis={analysis} documentId={documentId} />
    </LanguageProvider>
  );
}

// aria-label is "<title> · <count> — Expand/Collapse" (see SectionToggleHeader).
const sectionToggle = (title: string) => screen.getByRole("button", { name: new RegExp(`^${title} ·`) });

describe("AnalysisCard mandatory action chip", () => {
  // ANALYSIS has one mandatory action, so Actions auto-expands (see the disclosure rules) —
  // no need to open it manually in these three.
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

  it("renders an 'Add to calendar' link for each dated deadline, once Deadlines is expanded", () => {
    // A deadline far in the future so the auto-expand-if-due-within-7-days rule doesn't fire —
    // this test is about the link rendering once visible, not about the auto-expand rule itself
    // (see the "auto-expand" describe block below for that).
    const withDeadline: AnalysisResult = {
      ...ANALYSIS,
      deadlines: [{ date: "2099-08-12T00:00:00Z", description: LOC("Payment due") }],
    };
    renderCard(withDeadline, "doc-1");
    expect(screen.queryByText("Add to calendar")).not.toBeInTheDocument();

    fireEvent.click(sectionToggle("Important dates"));
    expect(screen.getByText("Add to calendar")).toBeInTheDocument();
  });
});

describe("AnalysisCard progressive disclosure", () => {
  beforeEach(() => window.localStorage.clear());

  it("Key points is collapsed by default and expands on tap", () => {
    renderCard(ANALYSIS, "doc-1");
    expect(screen.queryByText("Point one")).not.toBeInTheDocument();

    fireEvent.click(sectionToggle("Key points"));
    expect(screen.getByText("Point one")).toBeInTheDocument();
  });

  it("Actions auto-expands when a mandatory action is present", () => {
    renderCard(ANALYSIS, "doc-1");
    expect(screen.getByText("Pay the fee")).toBeInTheDocument();
  });

  it("Actions stays collapsed when nothing is mandatory", () => {
    const noneMandatory: AnalysisResult = {
      ...ANALYSIS,
      requiredActions: [{ description: LOC("Optional step"), isMandatory: false }],
    };
    renderCard(noneMandatory, "doc-1");
    expect(screen.queryByText("Optional step")).not.toBeInTheDocument();
  });

  it("Deadlines auto-expands for a deadline due within 7 days", () => {
    const soon = new Date();
    soon.setDate(soon.getDate() + 2);
    const dueSoon: AnalysisResult = {
      ...ANALYSIS,
      deadlines: [{ date: soon.toISOString(), description: LOC("Payment due") }],
    };
    renderCard(dueSoon, "doc-1");
    expect(screen.getByText("Add to calendar")).toBeInTheDocument();
  });

  it("a manual collapse survives a remount, overriding the auto-expand rule", () => {
    const { unmount } = renderCard(ANALYSIS, "doc-1");
    // Actions is auto-expanded (mandatory action present) — manually collapse it.
    fireEvent.click(sectionToggle("Required actions"));
    expect(screen.queryByText("Pay the fee")).not.toBeInTheDocument();
    unmount();

    renderCard(ANALYSIS, "doc-1");
    expect(screen.queryByText("Pay the fee")).not.toBeInTheDocument();
  });

  it("each collapsed section still shows its own audio button", () => {
    renderCard(ANALYSIS, "doc-1");
    // Key points is collapsed here, but its play button lives in the header, outside the
    // collapsed content.
    expect(screen.getByLabelText("Listen to key points")).toBeInTheDocument();
  });
});

describe("AnalysisCard verdict band", () => {
  it("shows the document type, urgency, and mandatory-action count", () => {
    renderCard();
    const verdict = screen.getByTestId("analysis-verdict");
    expect(verdict).toHaveTextContent("Letter");
    expect(verdict).toHaveTextContent("Medium");
    expect(verdict).toHaveTextContent("1 things you must do");
  });

  it("shows the nearest deadline with a relative-day label", () => {
    const soon = new Date();
    soon.setDate(soon.getDate() + 1);
    const withDeadline: AnalysisResult = { ...ANALYSIS, deadlines: [{ date: soon.toISOString(), description: LOC("Payment due") }] };
    renderCard(withDeadline);
    expect(screen.getByTestId("analysis-verdict")).toHaveTextContent("Due tomorrow");
  });
});
