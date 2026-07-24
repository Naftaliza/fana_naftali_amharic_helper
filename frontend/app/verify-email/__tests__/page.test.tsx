import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import VerifyEmailPage from "@/app/verify-email/page";
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

// Mock only the network boundary — tokenStore/AuthProvider's own logic stays real so
// verifyEmail's token-storage side effect is exercised for real.
jest.mock("@/lib/api", () => {
  const actual = jest.requireActual("@/lib/api");
  return { ...actual, api: { verifyEmail: jest.fn(), me: jest.fn(), resendVerification: jest.fn() } };
});

const mockedApi = api as jest.Mocked<Pick<typeof api, "verifyEmail" | "me" | "resendVerification">>;

const AUTH_RESPONSE = {
  accessToken: "access-token",
  refreshToken: "refresh-token",
  user: { id: "1", email: "user@test.local", displayName: "User", preferredLanguage: 0 },
};

function setUrl(search: string) {
  window.history.pushState({}, "", `/verify-email${search}`);
}

function renderPage() {
  return render(
    <LanguageProvider>
      <AuthProvider>
        <VerifyEmailPage />
      </AuthProvider>
    </LanguageProvider>
  );
}

describe("VerifyEmailPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it("verifies with the email/token from the URL and redirects to /dashboard on success", async () => {
    setUrl("?email=user%40test.local&token=abc123");
    mockedApi.verifyEmail.mockResolvedValue(AUTH_RESPONSE);
    mockedApi.me.mockResolvedValue(AUTH_RESPONSE.user as never);
    renderPage();

    await waitFor(() => expect(mockedApi.verifyEmail).toHaveBeenCalledWith("user@test.local", "abc123"));
    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/dashboard"), { timeout: 3000 });
    expect(window.localStorage.getItem("accessToken")).toBe("access-token");
  });

  it("calls verifyEmail only once even if effects re-run", async () => {
    setUrl("?email=user%40test.local&token=abc123");
    mockedApi.verifyEmail.mockResolvedValue(AUTH_RESPONSE);
    mockedApi.me.mockResolvedValue(AUTH_RESPONSE.user as never);
    renderPage();

    await waitFor(() => expect(mockPush).toHaveBeenCalled(), { timeout: 3000 });
    expect(mockedApi.verifyEmail).toHaveBeenCalledTimes(1);
  });

  it("shows an invalid-link error and a working resend button when verification fails", async () => {
    setUrl("?email=user%40test.local&token=bad-token");
    mockedApi.verifyEmail.mockRejectedValue(new Error("Invalid or expired verification link."));
    renderPage();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(mockPush).not.toHaveBeenCalled();

    mockedApi.resendVerification.mockResolvedValue({ message: "sent" });
    fireEvent.click(screen.getByRole("button", { name: /resend verification|שליחה חוזרת/i }));

    await waitFor(() => expect(mockedApi.resendVerification).toHaveBeenCalledWith("user@test.local"));
  });

  it("shows an invalid-link error when email/token are missing from the URL", async () => {
    setUrl("");
    renderPage();

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(mockedApi.verifyEmail).not.toHaveBeenCalled();
  });
});
