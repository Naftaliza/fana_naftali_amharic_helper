import { render, screen, fireEvent } from "@testing-library/react";
import { DocumentShareButton } from "@/components/DocumentShareButton";
import { LanguageProvider } from "@/lib/language-context";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });
const ANALYSIS: AnalysisResult = {
  summary: LOC("Summary"), documentType: LOC("Letter"), urgencyLevel: "Low",
  keyPoints: [], requiredActions: [], deadlines: [], explanation: LOC("Explanation"),
};

function renderButton() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <DocumentShareButton analysis={ANALYSIS} />
    </LanguageProvider>
  );
}

describe("DocumentShareButton", () => {
  const originalShare = (navigator as any).share;
  const originalOpen = window.open;
  const originalPrint = window.print;

  afterEach(() => {
    (navigator as any).share = originalShare;
    window.open = originalOpen;
    window.print = originalPrint;
    jest.restoreAllMocks();
  });

  it("renders Send and Print buttons", () => {
    renderButton();
    expect(screen.getByText("Send")).toBeInTheDocument();
    expect(screen.getByText("Print")).toBeInTheDocument();
  });

  it("uses navigator.share with the plain-text summary when available", async () => {
    const share = jest.fn().mockResolvedValue(undefined);
    (navigator as any).share = share;
    renderButton();

    fireEvent.click(screen.getByText("Send"));
    await Promise.resolve();

    expect(share).toHaveBeenCalledTimes(1);
    const arg = share.mock.calls[0][0];
    expect(arg.text).toContain("Letter");
    expect(arg.text).toContain("Summary");
  });

  it("falls back to a WhatsApp deep link when navigator.share is unavailable", () => {
    (navigator as any).share = undefined;
    window.open = jest.fn();
    renderButton();

    fireEvent.click(screen.getByText("Send"));

    expect(window.open).toHaveBeenCalledWith(
      expect.stringContaining("https://wa.me/?text="),
      "_blank",
      "noopener"
    );
  });

  it("calls window.print when Print is clicked", () => {
    window.print = jest.fn();
    renderButton();

    fireEvent.click(screen.getByText("Print"));

    expect(window.print).toHaveBeenCalledTimes(1);
  });
});
