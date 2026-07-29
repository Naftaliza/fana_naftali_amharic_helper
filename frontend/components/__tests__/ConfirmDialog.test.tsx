import { render, screen, fireEvent } from "@testing-library/react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { LanguageProvider } from "@/lib/language-context";

function renderDialog(props: Partial<React.ComponentProps<typeof ConfirmDialog>> = {}) {
  const onConfirm = jest.fn();
  const onCancel = jest.fn();
  render(
    <LanguageProvider>
      <ConfirmDialog
        open
        title="Delete this document?"
        body="This cannot be undone."
        confirmLabel="Delete document"
        onConfirm={onConfirm}
        onCancel={onCancel}
        {...props}
      />
    </LanguageProvider>
  );
  return { onConfirm, onCancel };
}

describe("ConfirmDialog", () => {
  it("renders nothing when closed", () => {
    render(
      <LanguageProvider>
        <ConfirmDialog
          open={false}
          title="x" body="y" confirmLabel="z"
          onConfirm={jest.fn()} onCancel={jest.fn()}
        />
      </LanguageProvider>
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("shows title, body, and a real verb on the confirm button — never OK/Cancel", () => {
    renderDialog();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Delete this document?")).toBeInTheDocument();
    expect(screen.getByText("This cannot be undone.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete document" })).toBeInTheDocument();
    expect(screen.queryByText("OK")).not.toBeInTheDocument();
  });

  it("calls onConfirm when the confirm button is clicked", () => {
    const { onConfirm } = renderDialog();
    fireEvent.click(screen.getByRole("button", { name: "Delete document" }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("calls onCancel when the cancel button is clicked", () => {
    const { onCancel } = renderDialog();
    fireEvent.click(screen.getByRole("button", { name: "ביטול" }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("is a labeled, modal dialog", () => {
    renderDialog();
    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAttribute("aria-label", "Delete this document?");
  });

  it("renders the destructive variant in red", () => {
    renderDialog({ destructive: true });
    expect(screen.getByRole("button", { name: "Delete document" }).className).toMatch(/red/);
  });
});
