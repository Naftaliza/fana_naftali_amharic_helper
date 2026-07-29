import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import AdminProvidersPage from "@/app/admin/providers/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: {
    adminListPending: jest.fn(),
    adminListProviders: jest.fn(),
    adminLeads: jest.fn(),
    adminReject: jest.fn(),
  },
}));
jest.mock("next/navigation", () => ({ useRouter: () => ({ replace: jest.fn() }) }));
const AUTH_USER = { id: "u1", email: "a@b.com", isAdmin: true };
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: AUTH_USER, loading: false }),
}));

const PROVIDER = {
  id: "p1", category: 0, displayName: "Acme Legal", city: null, phone: null, whatsApp: null,
  contactEmail: null, blurb: { he: "b", am: "b", en: "b" }, isActive: true, priority: 0, pricePerLead: 0,
};

function renderPage() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><AdminProvidersPage /></LanguageProvider>);
}

describe("AdminProvidersPage manage-tab delete flow", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (api.adminListPending as jest.Mock).mockResolvedValue([]);
    (api.adminListProviders as jest.Mock).mockResolvedValue([PROVIDER]);
  });

  it("shows a ConfirmDialog with provider-specific copy instead of window.confirm", async () => {
    renderPage();
    fireEvent.click(screen.getByText("Live providers"));
    await waitFor(() => screen.getByText("Acme Legal"));

    fireEvent.click(screen.getByText("Delete"));

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Permanently remove this provider?")).toBeInTheDocument();
    expect(api.adminReject).not.toHaveBeenCalled();
  });

  it("removes the provider only once confirmed", async () => {
    (api.adminReject as jest.Mock).mockResolvedValue({ ok: true });
    renderPage();
    fireEvent.click(screen.getByText("Live providers"));
    await waitFor(() => screen.getByText("Acme Legal"));

    fireEvent.click(screen.getByText("Delete"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(api.adminReject).toHaveBeenCalledWith("p1"));
  });
});
