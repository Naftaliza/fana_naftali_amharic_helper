import { render, screen } from "@testing-library/react";
import { ComingUpStrip } from "@/components/ComingUpStrip";
import { LanguageProvider } from "@/lib/language-context";
import type { DocumentSummary } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });

function doc(id: string, fileName: string, deadlines: DocumentSummary["deadlines"]): DocumentSummary {
  return { id, fileName, contentType: "application/pdf", uploadedAt: new Date().toISOString(), hasAnalysis: true, status: 2, deadlines };
}

function renderStrip(docs: DocumentSummary[]) {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><ComingUpStrip docs={docs} /></LanguageProvider>);
}

describe("ComingUpStrip", () => {
  it("renders nothing when there are no upcoming deadlines", () => {
    const { container } = renderStrip([doc("d1", "a.pdf", [])]);
    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing when every deadline is in the past", () => {
    const { container } = renderStrip([doc("d1", "a.pdf", [{ date: "2000-01-01T00:00:00Z", description: LOC("Pay") }])]);
    expect(container).toBeEmptyDOMElement();
  });

  it("lists upcoming deadlines across documents, nearest first", () => {
    renderStrip([
      doc("d1", "Later.pdf", [{ date: "2999-06-01T00:00:00Z", description: LOC("Renew") }]),
      doc("d2", "Sooner.pdf", [{ date: "2999-01-01T00:00:00Z", description: LOC("Pay") }]),
    ]);
    const items = screen.getAllByRole("listitem");
    expect(items[0].textContent).toContain("Sooner.pdf");
    expect(items[1].textContent).toContain("Later.pdf");
  });

  it("links each entry to its document", () => {
    renderStrip([doc("d1", "a.pdf", [{ date: "2999-01-01T00:00:00Z", description: LOC("Pay") }])]);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/documents/d1");
  });
});
