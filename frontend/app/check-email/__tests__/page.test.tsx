import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import CheckEmailPage from "@/app/check-email/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("next/link", () => {
  const Link = ({ children }: { children: React.ReactNode }) => <>{children}</>;
  Link.displayName = "Link";
  return Link;
});

jest.mock("@/lib/api", () => ({
  api: { resendVerification: jest.fn() },
}));

const mockedApi = api as jest.Mocked<typeof api>;

function setUrl(search: string) {
  window.history.pushState({}, "", `/check-email${search}`);
}

function renderPage() {
  return render(
    <LanguageProvider>
      <CheckEmailPage />
    </LanguageProvider>
  );
}

describe("CheckEmailPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("displays the email address from the query string", async () => {
    setUrl("?email=user%40test.local");
    renderPage();

    await screen.findByText("user@test.local");
  });

  it("resends the verification email and shows a confirmation", async () => {
    setUrl("?email=user%40test.local");
    mockedApi.resendVerification.mockResolvedValue({ message: "sent" });
    renderPage();

    const resendButton = await screen.findByRole("button", { name: /resend verification|שליחה חוזרת/i });
    fireEvent.click(resendButton);

    await waitFor(() => expect(mockedApi.resendVerification).toHaveBeenCalledWith("user@test.local"));
    await screen.findByText(/new verification link|קישור אימות חדש/i);
  });

  it("shows a rate-limited error without crashing", async () => {
    setUrl("?email=user%40test.local");
    mockedApi.resendVerification.mockRejectedValue(new Error("RATE_LIMITED"));
    renderPage();

    const resendButton = await screen.findByRole("button", { name: /resend verification|שליחה חוזרת/i });
    fireEvent.click(resendButton);

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
  });
});
