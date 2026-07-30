import { render, screen, act } from "@testing-library/react";
import { OfflineBanner } from "@/components/OfflineBanner";
import { LanguageProvider } from "@/lib/language-context";

function renderBanner() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><OfflineBanner /></LanguageProvider>);
}

describe("OfflineBanner", () => {
  afterEach(() => {
    Object.defineProperty(window.navigator, "onLine", { value: true, configurable: true });
  });

  it("renders nothing while online", () => {
    const { container } = renderBanner();
    expect(container).toBeEmptyDOMElement();
  });

  it("shows a status banner when the offline event fires", () => {
    renderBanner();
    act(() => window.dispatchEvent(new Event("offline")));
    expect(screen.getByRole("status")).toHaveTextContent(/No internet connection/);
  });

  it("hides again once back online", () => {
    const { container } = renderBanner();
    act(() => window.dispatchEvent(new Event("offline")));
    act(() => window.dispatchEvent(new Event("online")));
    expect(container).toBeEmptyDOMElement();
  });
});
