import { render, screen, act, fireEvent, waitFor } from "@testing-library/react";
import { UploadExperience } from "@/components/UploadExperience";
import { LanguageProvider } from "@/lib/language-context";
import { OrganizationProvider } from "@/lib/organization-context";
import { getPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({ api: { trialAnalyze: jest.fn(), listDocuments: jest.fn().mockResolvedValue([]) } }));
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
let mockUser: { id: string } | null = null;
jest.mock("@/lib/auth-context", () => ({ useAuth: () => ({ user: mockUser, loading: false }) }));

function renderExperience() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <OrganizationProvider><UploadExperience /></OrganizationProvider>
    </LanguageProvider>
  );
}

describe("UploadExperience offline handling", () => {
  afterEach(() => {
    Object.defineProperty(window.navigator, "onLine", { value: true, configurable: true });
  });

  it("disables the camera and file-choice entry points while offline", () => {
    renderExperience();
    act(() => window.dispatchEvent(new Event("offline")));

    expect(screen.getByText("Photograph a document").closest("button")).toBeDisabled();
    expect(screen.getByText("or choose an existing file").closest("button")).toBeDisabled();
    expect(screen.getByText(/need an internet connection/)).toBeInTheDocument();
  });

  it("re-enables them once back online", () => {
    renderExperience();
    act(() => window.dispatchEvent(new Event("offline")));
    act(() => window.dispatchEvent(new Event("online")));

    expect(screen.getByText("Photograph a document").closest("button")).not.toBeDisabled();
  });
});

const TRIAL_ANALYSIS = {
  summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "Letter" },
  urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
  explanation: { he: "e", am: "e", en: "e" },
};

describe("UploadExperience trial persistence", () => {
  beforeEach(() => { jest.clearAllMocks(); window.localStorage.removeItem("pendingTrialAnalysis"); });

  it("stashes the trial analysis when the register link is clicked", async () => {
    (api.trialAnalyze as jest.Mock).mockResolvedValue(TRIAL_ANALYSIS);
    renderExperience();

    const file = new File(["x"], "doc.jpg", { type: "image/jpeg" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => screen.getByText("Register"));

    fireEvent.click(screen.getByText("Register"));

    expect(getPendingTrialAnalysis()?.analysis).toEqual(TRIAL_ANALYSIS);
  });
});

describe("UploadExperience signed-in view", () => {
  beforeEach(() => { mockUser = { id: "u1" }; });
  afterEach(() => { mockUser = null; });

  it("skips the marketing headline for a returning signed-in visitor", () => {
    renderExperience();
    expect(screen.queryByText("Understand Hebrew Documents in Amharic")).not.toBeInTheDocument();
    // The camera button is still the first thing offered.
    expect(screen.getByText("Photograph a document")).toBeInTheDocument();
  });
});
