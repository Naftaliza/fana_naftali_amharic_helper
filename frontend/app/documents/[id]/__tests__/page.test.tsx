import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import DocumentDetailPage from "@/app/documents/[id]/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { getDocument: jest.fn(), deleteDocument: jest.fn(), analyze: jest.fn() },
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
