import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import AdminOrganizationsPage from "@/app/admin/organizations/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { adminListOrganizations: jest.fn(), adminSetOrganizationStatus: jest.fn() },
}));
jest.mock("next/navigation", () => ({ useRouter: () => ({ replace: jest.fn() }) }));
jest.mock("@/lib/theme-context", () => ({ useTheme: () => ({ theme: "light" }) }));
const AUTH_USER = { id: "u1", email: "a@b.com", isAdmin: true };
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: AUTH_USER, loading: false }),
}));

const ACTIVE_ORG = {
  id: "o1", name: "Acme", slug: "acme", isActive: true, createdAt: new Date().toISOString(),
  logoUrl: null, primaryColorHex: "#2563EB", accentColorHex: null, welcomeText: "hi",
};
const INACTIVE_ORG = { ...ACTIVE_ORG, id: "o2", name: "Beta", slug: "beta", isActive: false };

function renderPage() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><AdminOrganizationsPage /></LanguageProvider>);
}

describe("AdminOrganizationsPage toggleStatus", () => {
  beforeEach(() => jest.clearAllMocks());

  it("shows a ConfirmDialog before deactivating an active organization", async () => {
    (api.adminListOrganizations as jest.Mock).mockResolvedValue([ACTIVE_ORG]);
    renderPage();
    await waitFor(() => screen.getByText("Acme"));

    fireEvent.click(screen.getByText("Deactivate"));

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.adminSetOrganizationStatus).not.toHaveBeenCalled();
  });

  it("deactivates only once confirmed", async () => {
    (api.adminListOrganizations as jest.Mock).mockResolvedValue([ACTIVE_ORG]);
    (api.adminSetOrganizationStatus as jest.Mock).mockResolvedValue({ ok: true });
    renderPage();
    await waitFor(() => screen.getByText("Acme"));

    fireEvent.click(screen.getByText("Deactivate"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Deactivate" }));

    await waitFor(() => expect(api.adminSetOrganizationStatus).toHaveBeenCalledWith("o1", false));
  });

  it("reactivates without any confirmation dialog", async () => {
    (api.adminListOrganizations as jest.Mock).mockResolvedValue([INACTIVE_ORG]);
    (api.adminSetOrganizationStatus as jest.Mock).mockResolvedValue({ ok: true });
    renderPage();
    await waitFor(() => screen.getByText("Beta"));

    fireEvent.click(screen.getByText("Reactivate"));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await waitFor(() => expect(api.adminSetOrganizationStatus).toHaveBeenCalledWith("o2", true));
  });
});
