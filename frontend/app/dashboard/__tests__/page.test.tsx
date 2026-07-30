import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import DashboardPage from "@/app/dashboard/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { listDocuments: jest.fn(), deleteDocument: jest.fn() },
}));
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
const AUTH_USER = { id: "u1", email: "a@b.com" };
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: AUTH_USER, loading: false }),
}));

const DOC = {
  id: "d1", fileName: "letter.pdf", contentType: "application/pdf",
  uploadedAt: new Date().toISOString(), hasAnalysis: true, status: 2, deadlines: [],
};

function renderPage() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <DashboardPage />
    </LanguageProvider>
  );
}

describe("DashboardPage delete flow", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (api.listDocuments as jest.Mock).mockResolvedValue([DOC]);
  });

  it("shows a ConfirmDialog instead of window.confirm before deleting", async () => {
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByLabelText("Delete document"));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.deleteDocument).not.toHaveBeenCalled();
  });

  it("deletes the document only after the dialog is confirmed", async () => {
    (api.deleteDocument as jest.Mock).mockResolvedValue(undefined);
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByLabelText("Delete document"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Delete document" }));

    await waitFor(() => expect(api.deleteDocument).toHaveBeenCalledWith("d1"));
    await waitFor(() => expect(screen.queryByText("letter.pdf")).not.toBeInTheDocument());
  });

  it("shows a visible error and keeps the row when delete fails", async () => {
    (api.deleteDocument as jest.Mock).mockRejectedValue(new Error("boom"));
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByLabelText("Delete document"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Delete document" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByText("letter.pdf")).toBeInTheDocument();
  });

  it("shows the Coming up strip when a document has an upcoming deadline", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([
      { ...DOC, deadlines: [{ date: "2999-01-01T00:00:00Z", description: { he: "x", am: "x", en: "Pay" } }] },
    ]);
    renderPage();
    await waitFor(() => screen.getByText("Coming up"));
  });

  it("shows a distinct error with retry when the document list fails to load", async () => {
    (api.listDocuments as jest.Mock).mockRejectedValue(new Error("NETWORK_ERROR"));
    renderPage();
    await waitFor(() => screen.getByRole("alert"));
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
    expect(screen.queryByText(DOC.fileName)).not.toBeInTheDocument();
  });

  it("retries the fetch when 'Try again' is clicked", async () => {
    (api.listDocuments as jest.Mock)
      .mockRejectedValueOnce(new Error("NETWORK_ERROR"))
      .mockResolvedValueOnce([DOC]);
    renderPage();
    await waitFor(() => screen.getByRole("alert"));

    fireEvent.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => screen.getByText("letter.pdf"));
    expect(api.listDocuments).toHaveBeenCalledTimes(2);
  });

  it("still shows the plain empty state (not an error) when the list genuinely has zero documents", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([]);
    renderPage();
    await waitFor(() => expect(screen.queryByText("Loading...")).not.toBeInTheDocument());
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
