import { render, screen, waitFor } from "@testing-library/react";
import { RecentDocuments } from "@/components/RecentDocuments";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";
import { DOCUMENT_STATUS } from "@/lib/types";

jest.mock("@/lib/api", () => ({ api: { listDocuments: jest.fn() } }));

let mockUser: { id: string } | null = { id: "u1" };
jest.mock("@/lib/auth-context", () => ({ useAuth: () => ({ user: mockUser }) }));

function renderIt() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><RecentDocuments /></LanguageProvider>);
}

const doc = (i: number, overrides = {}) => ({
  id: `d${i}`, fileName: `letter-${i}.pdf`, contentType: "application/pdf",
  uploadedAt: new Date().toISOString(), hasAnalysis: true, status: DOCUMENT_STATUS.Ready,
  deadlines: [], ...overrides,
});

describe("RecentDocuments", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUser = { id: "u1" };
  });

  it("renders nothing for an anonymous visitor", () => {
    mockUser = null;
    const { container } = renderIt();
    expect(container).toBeEmptyDOMElement();
    expect(api.listDocuments).not.toHaveBeenCalled();
  });

  it("renders nothing when the account has zero documents", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([]);
    const { container } = renderIt();
    await waitFor(() => expect(api.listDocuments).toHaveBeenCalled());
    expect(container).toBeEmptyDOMElement();
  });

  it("shows at most 3 documents, even when more are returned", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([doc(1), doc(2), doc(3), doc(4)]);
    renderIt();
    await waitFor(() => screen.getByText("letter-1.pdf"));
    expect(screen.getByText("letter-3.pdf")).toBeInTheDocument();
    expect(screen.queryByText("letter-4.pdf")).not.toBeInTheDocument();
  });

  it("links each row to its document, and the header to the dashboard", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([doc(1)]);
    renderIt();
    await waitFor(() => screen.getByText("letter-1.pdf"));
    expect(screen.getByText("letter-1.pdf").closest("a")).toHaveAttribute("href", "/documents/d1");
    expect(screen.getByText("View all").closest("a")).toHaveAttribute("href", "/dashboard");
  });

  it("labels a same-day upload as 'Today' rather than a raw timestamp", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([doc(1)]);
    renderIt();
    await waitFor(() => screen.getByText(/^Today,/));
  });
});
