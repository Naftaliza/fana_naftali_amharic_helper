import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import { InvoiceCell } from "@/app/admin/providers/InvoiceCell";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";
import type { Invoice } from "@/lib/types";

jest.mock("@/lib/api", () => ({
  api: {
    adminInvoiceStatus: jest.fn(),
    adminGenerateInvoice: jest.fn(),
    adminInvoicePdf: jest.fn(),
  },
}));

const mockedApi = api as jest.Mocked<typeof api>;

function renderCell(props: { providerId?: string; year?: number; month?: number; hasEmail?: boolean } = {}) {
  return render(
    <LanguageProvider>
      <InvoiceCell providerId={props.providerId ?? "p1"} year={props.year ?? 2026} month={props.month ?? 7} hasEmail={props.hasEmail ?? true} />
    </LanguageProvider>
  );
}

const invoice: Invoice = {
  id: "inv-1", providerId: "p1", invoiceNumber: "INV-1", periodYear: 2026, periodMonth: 7,
  currency: "ILS", totalAmount: 100, leadCount: 2, status: 1, sentToEmail: "x@test.local",
  generatedAt: new Date().toISOString(), sentAt: new Date().toISOString(), sendError: null,
};

describe("InvoiceCell", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("shows no button while loading, then resolves to the generate button (the fixed 204/undefined bug)", async () => {
    // Simulates the real contract: no invoice yet round-trips through request() as `undefined`,
    // not `null` — this is exactly the value that used to get stuck as the loading sentinel too.
    mockedApi.adminInvoiceStatus.mockResolvedValue(undefined as unknown as Invoice | null);

    renderCell();

    // While the promise is unresolved, no button should be rendered yet.
    expect(screen.queryByRole("button")).toBeNull();

    // It must resolve to the generate button, not stay stuck showing the loading state forever.
    await waitFor(() => expect(screen.getByRole("button")).toBeInTheDocument());
  });

  it("disables the button with a tooltip when the provider has no contact email", async () => {
    mockedApi.adminInvoiceStatus.mockResolvedValue(null);

    renderCell({ hasEmail: false });

    const button = await screen.findByRole("button");
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("title");
  });

  it("shows a ConfirmDialog instead of window.confirm before generating", async () => {
    mockedApi.adminInvoiceStatus.mockResolvedValue(null);
    renderCell({ hasEmail: true });

    const button = await screen.findByRole("button");
    fireEvent.click(button);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(mockedApi.adminGenerateInvoice).not.toHaveBeenCalled();
  });

  it("generates an invoice once confirmed and shows the resulting status badge", async () => {
    mockedApi.adminInvoiceStatus.mockResolvedValue(null);
    mockedApi.adminGenerateInvoice.mockResolvedValue(invoice);

    renderCell({ hasEmail: true });

    const button = await screen.findByRole("button");
    expect(button).toBeEnabled();
    fireEvent.click(button);

    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /generate|חשבונית/i }));

    await waitFor(() => expect(mockedApi.adminGenerateInvoice).toHaveBeenCalledWith("p1", 2026, 7));
    // The generate button is replaced by the status badge once an invoice exists.
    await waitFor(() => expect(screen.queryByRole("button", { name: /.*generate.*|.*חשבונית.*/i })).toBeNull());
  });

  it("renders the status badge immediately when an invoice already exists for the period", async () => {
    mockedApi.adminInvoiceStatus.mockResolvedValue(invoice);

    renderCell();

    // No "generate" button should ever appear — only the view-PDF link/badge.
    await waitFor(() => expect(screen.getByRole("button", { name: /pdf/i })).toBeInTheDocument());
  });
});
