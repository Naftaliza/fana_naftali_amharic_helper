import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import RegisterPage from "@/app/register/page";
import { LanguageProvider } from "@/lib/language-context";
import { AuthProvider } from "@/lib/auth-context";
import { OrganizationProvider } from "@/lib/organization-context";
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

// Mock only the network boundary — tokenStore/OrganizationProvider's fetch logic stay real.
jest.mock("@/lib/api", () => {
  const actual = jest.requireActual("@/lib/api");
  return { ...actual, api: { register: jest.fn(), me: jest.fn(), getOrganization: jest.fn() } };
});

const mockedApi = api as jest.Mocked<Pick<typeof api, "register" | "me" | "getOrganization">>;

function renderPage() {
  return render(
    <LanguageProvider>
      <AuthProvider>
        <OrganizationProvider>
          <RegisterPage />
        </OrganizationProvider>
      </AuthProvider>
    </LanguageProvider>
  );
}

const REGISTER_RESPONSE = {
  message: "Account created. Check your email to verify your address and finish signing up.",
  email: "user@test.local",
};

function fillAndSubmit({ name = "User", email = "user@test.local", password = "correctpass123", confirm = password }: {
  name?: string; email?: string; password?: string; confirm?: string;
} = {}) {
  fireEvent.change(screen.getByLabelText(/full name|שם מלא/i), { target: { value: name } });
  fireEvent.change(screen.getByLabelText(/email|דוא"ל/i), { target: { value: email } });
  fireEvent.change(screen.getByLabelText(/^password$|^סיסמה$/i), { target: { value: password } });
  fireEvent.change(screen.getByLabelText(/confirm password|אימות סיסמה/i), { target: { value: confirm } });
  fireEvent.submit(screen.getByLabelText(/^password$|^סיסמה$/i).closest("form")!);
}

describe("RegisterPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.clear();
    window.sessionStorage.clear();
    window.history.pushState({}, "", "/register");
  });

  it("registers and redirects to /check-email on success, without fetching any org branding", async () => {
    mockedApi.register.mockResolvedValue(REGISTER_RESPONSE);
    renderPage();

    fillAndSubmit();

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/check-email?email=user%40test.local"));
    expect(mockedApi.getOrganization).not.toHaveBeenCalled();
    expect(mockedApi.me).not.toHaveBeenCalled();
  });

  it("shows an alert on duplicate email", async () => {
    mockedApi.register.mockRejectedValue(new Error("Email already registered."));
    renderPage();

    fillAndSubmit();

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Email already registered."));
  });

  it("shows a client-side mismatch error and never calls the API", async () => {
    renderPage();

    fillAndSubmit({ password: "correctpass123", confirm: "different123" });

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(mockedApi.register).not.toHaveBeenCalled();
  });

  it("toggles visibility of both the password and confirm-password fields together", () => {
    renderPage();
    const passwordInput = screen.getByLabelText(/^password$|^סיסמה$/i) as HTMLInputElement;
    const confirmInput = screen.getByLabelText(/confirm password|אימות סיסמה/i) as HTMLInputElement;
    expect(passwordInput.type).toBe("password");
    expect(confirmInput.type).toBe("password");

    fireEvent.click(screen.getByRole("button", { name: /show password|הצג סיסמה/i }));
    expect(passwordInput.type).toBe("text");
    expect(confirmInput.type).toBe("text");

    fireEvent.click(screen.getByRole("button", { name: /hide password|הסתר סיסמה/i }));
    expect(passwordInput.type).toBe("password");
    expect(confirmInput.type).toBe("password");
  });

  it("shows the tenant's logo and welcome text for an org-branded registration", async () => {
    window.history.pushState({}, "", "/register?org=acme");
    mockedApi.getOrganization.mockResolvedValue({
      slug: "acme",
      name: "Acme Corp",
      logoUrl: "https://example.com/logo.png",
      primaryColorHex: "#123456",
      accentColorHex: "#654321",
      welcomeText: { he: "ברוכים הבאים ל-Acme", am: "", en: "Welcome to Acme" },
    });

    renderPage();

    await screen.findByText("ברוכים הבאים ל-Acme");
    expect(screen.getByAltText("Acme Corp logo")).toHaveAttribute("src", "https://example.com/logo.png");
    expect(screen.getByText(/Acme Corp/)).toBeInTheDocument();
  });
});
