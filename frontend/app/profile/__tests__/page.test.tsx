import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import ProfilePage from "@/app/profile/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({ api: { deleteAccount: jest.fn(), exportAccount: jest.fn() } }));
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
const mockLogout = jest.fn();
const AUTH_USER = { id: "u1", email: "a@b.com", displayName: "A", preferredLanguage: 0 };
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: AUTH_USER, loading: false, logout: mockLogout }),
}));

function renderPage() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><ProfilePage /></LanguageProvider>);
}

describe("ProfilePage delete account flow", () => {
  beforeEach(() => jest.clearAllMocks());

  it("shows a ConfirmDialog instead of window.confirm", () => {
    renderPage();
    fireEvent.click(screen.getByText("Delete account"));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.deleteAccount).not.toHaveBeenCalled();
  });

  it("deletes the account only once confirmed", async () => {
    (api.deleteAccount as jest.Mock).mockResolvedValue(undefined);
    renderPage();
    fireEvent.click(screen.getByText("Delete account"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Delete account" }));
    await waitFor(() => expect(api.deleteAccount).toHaveBeenCalled());
    expect(mockLogout).toHaveBeenCalled();
  });
});
