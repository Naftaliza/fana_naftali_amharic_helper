import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import ResetPasswordPage from "@/app/reset-password/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

jest.mock("next/link", () => {
  const Link = ({ children }: { children: React.ReactNode }) => <>{children}</>;
  Link.displayName = "Link";
  return Link;
});

jest.mock("@/lib/api", () => ({
  api: { resetPassword: jest.fn() },
}));

const mockedApi = api as jest.Mocked<typeof api>;

function setUrl(search: string) {
  window.history.pushState({}, "", `/reset-password${search}`);
}

function renderPage() {
  return render(
    <LanguageProvider>
      <ResetPasswordPage />
    </LanguageProvider>
  );
}

describe("ResetPasswordPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("shows an invalid-link message (not stuck loading forever) when email/token are missing", async () => {
    // The exact bug fixed this session: null was used as both "params not read yet" and
    // "genuinely missing," so a bad link got stuck showing the loading state forever.
    setUrl("");
    renderPage();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.queryByLabelText(/new password|סיסמה חדשה/i)).toBeNull();
  });

  it("renders the reset form when a valid email/token are present", async () => {
    setUrl("?email=user%40test.local&token=abc123");
    renderPage();

    await waitFor(() => expect(screen.getByLabelText(/new password|סיסמה חדשה/i)).toBeInTheDocument());
  });

  it("shows a client-side error and does not call the API when passwords don't match", async () => {
    setUrl("?email=user%40test.local&token=abc123");
    renderPage();

    const newPasswordInput = await screen.findByLabelText(/new password|סיסמה חדשה/i);
    const confirmInput = screen.getByLabelText(/confirm password|אימות סיסמה/i);
    fireEvent.change(newPasswordInput, { target: { value: "password123" } });
    fireEvent.change(confirmInput, { target: { value: "different123" } });
    fireEvent.submit(newPasswordInput.closest("form")!);

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(mockedApi.resetPassword).not.toHaveBeenCalled();
  });

  it("submits with the exact email/token/newPassword and shows the done message", async () => {
    mockedApi.resetPassword.mockResolvedValue({ message: "Password updated." });
    setUrl("?email=user%40test.local&token=abc123");
    renderPage();

    const newPasswordInput = await screen.findByLabelText(/new password|סיסמה חדשה/i);
    const confirmInput = screen.getByLabelText(/confirm password|אימות סיסמה/i);
    fireEvent.change(newPasswordInput, { target: { value: "newpassword123" } });
    fireEvent.change(confirmInput, { target: { value: "newpassword123" } });
    fireEvent.submit(newPasswordInput.closest("form")!);

    await waitFor(() =>
      expect(mockedApi.resetPassword).toHaveBeenCalledWith("user@test.local", "abc123", "newpassword123")
    );
    await waitFor(() => expect(screen.queryByLabelText(/new password|סיסמה חדשה/i)).toBeNull());
  });
});
