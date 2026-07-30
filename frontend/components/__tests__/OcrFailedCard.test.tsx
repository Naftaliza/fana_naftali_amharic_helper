import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { OcrFailedCard } from "@/components/OcrFailedCard";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { uploadDocument: jest.fn(), deleteDocument: jest.fn() },
}));
const mockPush = jest.fn();
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: mockPush }) }));

function renderCard(props: Partial<React.ComponentProps<typeof OcrFailedCard>> = {}) {
  window.localStorage.setItem("lang", "en");
  const onRetryOcr = jest.fn();
  render(
    <LanguageProvider>
      <OcrFailedCard
        documentId="d1"
        rawError="No readable text was found in the document."
        onRetryOcr={onRetryOcr}
        retrying={false}
        {...props}
      />
    </LanguageProvider>
  );
  return { onRetryOcr };
}

describe("OcrFailedCard", () => {
  beforeEach(() => jest.clearAllMocks());

  it("shows the plain-language cause, not the raw backend string, as the primary message", () => {
    renderCard();
    expect(screen.getByRole("alert")).toHaveTextContent("We couldn't find any readable text");
    expect(screen.queryByText("No readable text was found in the document.")).not.toBeInTheDocument();
  });

  it("reveals the raw error behind a details toggle", () => {
    renderCard();
    fireEvent.click(screen.getByText("Technical details"));
    expect(screen.getByText("No readable text was found in the document.")).toBeInTheDocument();
  });

  it("calls onRetryOcr when 'Try again' is clicked", () => {
    const { onRetryOcr } = renderCard();
    fireEvent.click(screen.getByText("Try again"));
    expect(onRetryOcr).toHaveBeenCalledTimes(1);
  });

  it("uploads a replacement document and deletes the failed one when a file is chosen", async () => {
    (api.uploadDocument as jest.Mock).mockResolvedValue({ id: "new-doc" });
    (api.deleteDocument as jest.Mock).mockResolvedValue(undefined);
    renderCard();

    const file = new File(["x"], "retry.jpg", { type: "image/jpeg" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => expect(api.uploadDocument).toHaveBeenCalledWith([file]));
    await waitFor(() => expect(api.deleteDocument).toHaveBeenCalledWith("d1"));
    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/documents/new-doc"));
  });
});
