"use client";

import { useEffect, useState } from "react";
import { FileText } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import type { Invoice } from "@/lib/types";

// Per-provider "generate & send invoice" cell for the Leads tab's by-provider table. Split into
// its own file (rather than a local function in page.tsx) because a Next.js App Router page.tsx
// may only export the reserved names (default, metadata, etc.) — any other named export breaks
// the route's generated type-checking.
export function InvoiceCell({
  providerId, year, month, hasEmail,
}: { providerId: string; year: number; month: number; hasEmail: boolean }) {
  const { t } = useLanguage();
  const [loading, setLoading] = useState(true);
  // A 204 No Content (no invoice for this provider+month) round-trips through the shared
  // request() helper as `undefined`, not `null` — normalize both to null here rather than
  // relying on `undefined` as a loading sentinel, which would collide with that response.
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setInvoice(null);
    setError(null);
    api.adminInvoiceStatus(providerId, year, month)
      .then((inv) => setInvoice(inv ?? null))
      .catch(() => setInvoice(null))
      .finally(() => setLoading(false));
  }, [providerId, year, month]);

  const viewPdf = async (invoiceId: string) => {
    const blob = await api.adminInvoicePdf(providerId, invoiceId);
    const url = URL.createObjectURL(blob);
    window.open(url, "_blank");
    setTimeout(() => URL.revokeObjectURL(url), 30_000);
  };

  const generate = async () => {
    if (!window.confirm(t("invoice.confirm"))) return;
    setBusy(true);
    setError(null);
    try {
      const created = await api.adminGenerateInvoice(providerId, year, month);
      setInvoice(created);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <span className="text-xs text-gray-400">{t("common.loading")}</span>;

  if (invoice) {
    const sent = invoice.status === 1;
    return (
      <span className="inline-flex items-center gap-2">
        <span className={`rounded-full px-2 py-0.5 text-xs ${
          sent
            ? "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300"
            : "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300"
        }`}>
          {sent ? t("invoice.sent") : t("invoice.failed")}
        </span>
        <button onClick={() => viewPdf(invoice.id)} className="inline-flex items-center gap-1 text-xs text-brand hover:underline">
          <FileText className="h-3.5 w-3.5" />{t("invoice.viewPdf")}
        </button>
      </span>
    );
  }

  return (
    <span className="inline-flex flex-col items-end gap-1">
      <button
        onClick={generate}
        disabled={busy || !hasEmail}
        title={hasEmail ? undefined : t("invoice.noEmailTooltip")}
        className="inline-flex items-center gap-1 rounded-full border border-gray-300 px-3 py-1.5 text-xs font-medium hover:bg-gray-50 disabled:opacity-50 dark:border-gray-700 dark:hover:bg-gray-800"
      >
        {busy ? t("invoice.generating") : t("invoice.generate")}
      </button>
      {error && <span className="text-xs text-red-600">{error}</span>}
    </span>
  );
}
