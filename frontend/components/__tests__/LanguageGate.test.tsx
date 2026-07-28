import { render, screen, fireEvent } from "@testing-library/react";
import { LanguageGate } from "@/components/LanguageGate";
import { LanguageProvider } from "@/lib/language-context";

function renderGate() {
  return render(
    <LanguageProvider>
      <LanguageGate />
    </LanguageProvider>
  );
}

describe("LanguageGate", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("shows the picker when no language has been chosen yet", () => {
    renderGate();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("English")).toBeInTheDocument();
    expect(screen.getByText("አማርኛ")).toBeInTheDocument();
    expect(screen.getByText("עברית")).toBeInTheDocument();
  });

  it("stays hidden when a language is already stored", () => {
    window.localStorage.setItem("lang", "am");
    renderGate();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("persists the tapped language and closes", () => {
    renderGate();
    fireEvent.click(screen.getByText("አማርኛ"));

    expect(window.localStorage.getItem("lang")).toBe("am");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
