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
  uploadedAt: new Date().toISOString(), hasAnalysis: true, status: 2,
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
});
