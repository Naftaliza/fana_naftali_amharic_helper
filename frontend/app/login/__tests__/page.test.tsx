import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import LoginPage from "@/app/login/page";
import { LanguageProvider } from "@/lib/language-context";
import { AuthProvider } from "@/lib/auth-context";
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

// Mock only the network boundary (api.login/api.me) — tokenStore itself stays real so the
// remember-me storage-location assertions below exercise the actual implementation.
jest.mock("@/lib/api", () => {
  const actual = jest.requireActual("@/lib/api");
  return { ...actual, api: { login: jest.fn(), me: jest.fn(), resendVerification: jest.fn() } };
});

const mockedApi = api as jest.Mocked<Pick<typeof api, "login" | "me" | "resendVerification">>;

function renderPage() {
  return render(
    <LanguageProvider>
      <AuthProvider>
        <LoginPage />
      </AuthProvider>
    </LanguageProvider>
  );
}

const AUTH_RESPONSE = {
  accessToken: "access-token",
  refreshToken: "refresh-token",
  user: { id: "1", email: "user@test.local", displayName: "User", preferredLanguage: 0 },
};

describe("LoginPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it("logs in and redirects to /dashboard on success", async () => {
    mockedApi.login.mockResolvedValue(AUTH_RESPONSE);
    mockedApi.me.mockResolvedValue(AUTH_RESPONSE.user as never);
    renderPage();

    fireEvent.change(screen.getByLabelText(/email|דוא"ל/i), { target: { value: "user@test.local" } });
    fireEvent.change(screen.getByLabelText(/^password$|^סיסמה$/i), { target: { value: "correct-password" } });
    fireEvent.submit(screen.getByLabelText(/^password$|^סיסמה$/i).closest("form")!);

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/dashboard"));
  });

  it("shows an error alert when the credentials are rejected", async () => {
    mockedApi.login.mockRejectedValue(new Error("Invalid email or password."));
    renderPage();

    fireEvent.change(screen.getByLabelText(/email|דוא"ל/i), { target: { value: "user@test.local" } });
    fireEvent.change(screen.getByLabelText(/^password$|^סיסמה$/i), { target: { value: "wrong-password" } });
    fireEvent.submit(screen.getByLabelText(/^password$|^סיסמה$/i).closest("form")!);

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Invalid email or password."));
  });

  it("shows a resend-verification prompt (not the generic error) for an unverified account", async () => {
    mockedApi.login.mockRejectedValue(new Error("EMAIL_NOT_VERIFIED"));
    renderPage();

    fireEvent.change(screen.getByLabelText(/email|דוא"ל/i), { target: { value: "user@test.local" } });
    fireEvent.change(screen.getByLabelText(/^password$|^סיסמה$/i), { target: { value: "correct-password" } });
    fireEvent.submit(screen.getByLabelText(/^password$|^סיסמה$/i).closest("form")!);

    await waitFor(() => expect(screen.getByRole("alert")).not.toHaveTextContent("EMAIL_NOT_VERIFIED"));
    const resendButton = await screen.findByRole("button", { name: /resend verification|שליחה חוזרת/i });

    mockedApi.resendVerification.mockResolvedValue({ message: "sent" });
    fireEvent.click(resendButton);

    await waitFor(() => expect(mockedApi.resendVerification).toHaveBeenCalledWith("user@test.local"));
  });

  it("toggles password visibility", () => {
    renderPage();
    const passwordInput = screen.getByLabelText(/^password$|^סיסמה$/i) as HTMLInputElement;
    expect(passwordInput.type).toBe("password");

    fireEvent.click(screen.getByRole("button", { name: /show password|הצג סיסמה/i }));
    expect(passwordInput.type).toBe("text");

    fireEvent.click(screen.getByRole("button", { name: /hide password|הסתר סיסמה/i }));
    expect(passwordInput.type).toBe("password");
  });

  it("persists tokens to localStorage when remember-me is checked (default)", async () => {
    mockedApi.login.mockResolvedValue(AUTH_RESPONSE);
    mockedApi.me.mockResolvedValue(AUTH_RESPONSE.user as never);
    renderPage();

    fireEvent.change(screen.getByLabelText(/email|דוא"ל/i), { target: { value: "user@test.local" } });
    fireEvent.change(screen.getByLabelText(/^password$|^סיסמה$/i), { target: { value: "correct-password" } });
    fireEvent.submit(screen.getByLabelText(/^password$|^סיסמה$/i).closest("form")!);

    await waitFor(() => expect(mockPush).toHaveBeenCalled());
    expect(window.localStorage.getItem("accessToken")).toBe("access-token");
    expect(window.sessionStorage.getItem("accessToken")).toBeNull();
  });

  it("persists tokens to sessionStorage when remember-me is unchecked", async () => {
    mockedApi.login.mockResolvedValue(AUTH_RESPONSE);
    mockedApi.me.mockResolvedValue(AUTH_RESPONSE.user as never);
    renderPage();

    fireEvent.click(screen.getByRole("checkbox"));
    fireEvent.change(screen.getByLabelText(/email|דוא"ל/i), { target: { value: "user@test.local" } });
    fireEvent.change(screen.getByLabelText(/^password$|^סיסמה$/i), { target: { value: "correct-password" } });
    fireEvent.submit(screen.getByLabelText(/^password$|^סיסמה$/i).closest("form")!);

    await waitFor(() => expect(mockPush).toHaveBeenCalled());
    expect(window.sessionStorage.getItem("accessToken")).toBe("access-token");
    expect(window.localStorage.getItem("accessToken")).toBeNull();
  });
});
