import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SaveTrialAnalysisPrompt } from "@/components/SaveTrialAnalysisPrompt";
import { LanguageProvider } from "@/lib/language-context";
import { setPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({ api: { attachTrialAnalysis: jest.fn() } }));

let mockUser: { id: string } | null = null;
jest.mock("@/lib/auth-context", () => ({ useAuth: () => ({ user: mockUser, loading: false }) }));

const ANALYSIS = {
  summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "t" },
  urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
  explanation: { he: "e", am: "e", en: "e" },
};

function renderPrompt() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><SaveTrialAnalysisPrompt /></LanguageProvider>);
}

describe("SaveTrialAnalysisPrompt", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.removeItem("pendingTrialAnalysis");
    mockUser = null;
  });

  it("renders nothing when signed out, even with a stash present", () => {
    setPendingTrialAnalysis(ANALYSIS as any);
    const { container } = renderPrompt();
    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing when signed in with no stash", () => {
    mockUser = { id: "u1" };
    const { container } = renderPrompt();
    expect(container).toBeEmptyDOMElement();
  });

  it("offers to save when signed in with a stash present", () => {
    mockUser = { id: "u1" };
    setPendingTrialAnalysis(ANALYSIS as any);
    renderPrompt();
    expect(screen.getByText("Save the document you analyzed before signing up?")).toBeInTheDocument();
  });

  it("saves and clears the stash on confirm", async () => {
    mockUser = { id: "u1" };
    setPendingTrialAnalysis(ANALYSIS as any);
    (api.attachTrialAnalysis as jest.Mock).mockResolvedValue({ id: "d1" });
    renderPrompt();

    fireEvent.click(screen.getByText("Save"));

    await waitFor(() => expect(api.attachTrialAnalysis).toHaveBeenCalledWith(ANALYSIS));
    expect(window.localStorage.getItem("pendingTrialAnalysis")).toBeNull();
  });

  it("dismissing clears the stash without saving", () => {
    mockUser = { id: "u1" };
    setPendingTrialAnalysis(ANALYSIS as any);
    renderPrompt();

    fireEvent.click(screen.getByLabelText("Skip"));

    expect(api.attachTrialAnalysis).not.toHaveBeenCalled();
    expect(window.localStorage.getItem("pendingTrialAnalysis")).toBeNull();
  });
});
