import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import DocumentDetailPage from "@/app/documents/[id]/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { getDocument: jest.fn(), deleteDocument: jest.fn(), analyze: jest.fn(), retryOcr: jest.fn() },
}));
jest.mock("next/navigation", () => ({
  useParams: () => ({ id: "d1" }),
  useRouter: () => ({ push: jest.fn() }),
}));

const PENDING_DOC = {
  id: "d1", fileName: "letter.pdf", contentType: "application/pdf", ocrText: null,
  uploadedAt: new Date().toISOString(), analysis: null, status: 0,
  processedPages: 0, totalPages: 1, skippedPages: 0, processingError: null,
};

function renderPage() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><DocumentDetailPage /></LanguageProvider>);
}

describe("DocumentDetailPage cancel flow", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (api.getDocument as jest.Mock).mockResolvedValue(PENDING_DOC);
  });

  it("shows a ConfirmDialog instead of window.confirm before cancelling", async () => {
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByRole("button", { name: /Cancel/ }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.deleteDocument).not.toHaveBeenCalled();
  });

  it("deletes and navigates away only once confirmed", async () => {
    (api.deleteDocument as jest.Mock).mockResolvedValue(undefined);
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByRole("button", { name: /Cancel/ }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Delete document" }));

    await waitFor(() => expect(api.deleteDocument).toHaveBeenCalledWith("d1"));
  });
});

describe("DocumentDetailPage document actions", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (api.getDocument as jest.Mock).mockResolvedValue({
      ...PENDING_DOC,
      status: 2,
      analysis: {
        summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "t" },
        urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
        explanation: { he: "e", am: "e", en: "e" },
      },
    });
  });

  it("shows Send/Print document actions instead of the generic ShareButton once analyzed", async () => {
    renderPage();
    await waitFor(() => screen.getByText("Send"));
    expect(screen.getByText("Print")).toBeInTheDocument();
    expect(screen.queryByText("Tell a friend about Fana")).not.toBeInTheDocument();
  });
});

describe("DocumentDetailPage OCR failure recovery", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("shows OcrFailedCard with a plain-language cause when OCR failed", async () => {
    (api.getDocument as jest.Mock).mockResolvedValue({
      ...PENDING_DOC, status: 3, processingError: "No readable text was found in the document.",
    });
    renderPage();
    await waitFor(() => screen.getByRole("alert"));
    expect(screen.getByRole("alert")).toHaveTextContent(/couldn't find any readable text/i);
    expect(screen.getByText("Retake photo")).toBeInTheDocument();
  });

  it("re-runs OCR and resumes polling when 'Try again' is clicked", async () => {
    (api.getDocument as jest.Mock)
      .mockResolvedValueOnce({ ...PENDING_DOC, status: 3, processingError: "No readable text was found in the document." })
      .mockResolvedValueOnce({ ...PENDING_DOC, status: 0 })
      .mockResolvedValueOnce({ ...PENDING_DOC, status: 2, analysis: null });
    (api.retryOcr as jest.Mock).mockResolvedValue({ ok: true });
    renderPage();
    await waitFor(() => screen.getByText("Try again"));

    fireEvent.click(screen.getByText("Try again"));

    await waitFor(() => expect(api.retryOcr).toHaveBeenCalledWith("d1"));
    await waitFor(() => expect(api.getDocument).toHaveBeenCalledTimes(2), { timeout: 3000 });
  });
});
