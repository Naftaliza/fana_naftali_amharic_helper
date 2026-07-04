"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Check, Trash2, Mail, Phone, MessageCircle, MapPin, Pencil, X, Eye, EyeOff, ThumbsUp } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import {
  LEAD_STATUS_KEYS, loc, type LeadsOverview, type ManagedProvider,
  type PendingProvider, type RecentLead,
} from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { InvoiceCell } from "@/app/admin/providers/InvoiceCell";

const CATEGORY_KEYS = [
  "cat.government", "cat.bank", "cat.insurance", "cat.employment",
  "cat.healthcare", "cat.municipality", "cat.other",
];

const URGENCY_KEYS = ["urg.low", "urg.medium", "urg.high", "urg.critical"];

const inputCls =
  "h-11 w-full rounded-xl border border-gray-300 bg-white px-4 text-base focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100";

function CategoryBadge({ category, t }: { category: number; t: (k: string) => string }) {
  return (
    <span className="ms-2 rounded-full bg-brand-light px-2 py-0.5 text-xs text-brand dark:bg-brand/15">
      {t(CATEGORY_KEYS[category] ?? "cat.other")}
    </span>
  );
}

function ContactLine({ p }: { p: PendingProvider | ManagedProvider }) {
  return (
    <div className="flex flex-wrap gap-x-4 gap-y-1 pt-1 text-sm text-gray-500 dark:text-gray-400">
      {p.city && <span className="inline-flex items-center gap-1"><MapPin className="h-4 w-4" />{p.city}</span>}
      {p.contactEmail && <span className="inline-flex items-center gap-1"><Mail className="h-4 w-4" />{p.contactEmail}</span>}
      {p.phone && <span className="inline-flex items-center gap-1"><Phone className="h-4 w-4" />{p.phone}</span>}
      {p.whatsApp && <span className="inline-flex items-center gap-1"><MessageCircle className="h-4 w-4" />{p.whatsApp}</span>}
    </div>
  );
}

export default function AdminProvidersPage() {
  const { t, language, rtl } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [tab, setTab] = useState<"pending" | "live" | "leads">("pending");

  // Guard: only admins.
  useEffect(() => {
    if (loading) return;
    if (!user?.isAdmin) router.replace("/");
  }, [loading, user, router]);

  if (loading || !user?.isAdmin) {
    return <p className="pt-16 text-center text-gray-500">{t("common.loading")}</p>;
  }

  const tabBtn = (key: "pending" | "live" | "leads", label: string) => (
    <button
      onClick={() => setTab(key)}
      className={`rounded-full px-4 py-2 text-sm font-medium ${
        tab === key ? "bg-brand text-white" : "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"
      }`}
    >
      {label}
    </button>
  );

  return (
    <div className="mx-auto max-w-3xl space-y-5 pt-6" dir={rtl ? "rtl" : "ltr"}>
      <div className="flex flex-wrap gap-2">
        {tabBtn("pending", t("admin.tabPending"))}
        {tabBtn("live", t("admin.tabLive"))}
        {tabBtn("leads", t("admin.tabLeads"))}
      </div>
      {tab === "pending" ? <PendingTab /> : tab === "live" ? <ManageTab /> : <LeadsTab />}
    </div>
  );

  // ---- Pending review tab ----
  function PendingTab() {
    const [pending, setPending] = useState<PendingProvider[] | null>(null);
    const [busyId, setBusyId] = useState<string | null>(null);

    useEffect(() => { api.adminListPending().then(setPending).catch(() => setPending([])); }, []);

    const act = async (id: string, fn: () => Promise<unknown>) => {
      setBusyId(id);
      try { await fn(); setPending((l) => (l ?? []).filter((p) => p.id !== id)); }
      finally { setBusyId(null); }
    };

    if (pending === null) return <p className="text-gray-500">{t("common.loading")}</p>;
    if (pending.length === 0) return <p className="text-gray-500 dark:text-gray-400">{t("admin.empty")}</p>;

    return (
      <ul className="space-y-3">
        {pending.map((p) => (
          <li key={p.id}>
            <Card>
              <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="space-y-1">
                  <p className="font-semibold">{p.displayName}<CategoryBadge category={p.category} t={t} /></p>
                  <p className="text-sm text-gray-600 dark:text-gray-300">{loc(p.blurb, language)}</p>
                  <ContactLine p={p} />
                </div>
                <div className="flex shrink-0 gap-2">
                  <button onClick={() => act(p.id, () => api.adminApprove(p.id))} disabled={busyId === p.id}
                    className="inline-flex items-center gap-1 rounded-full bg-brand px-3 py-2 text-sm font-medium text-white hover:bg-brand-dark disabled:opacity-50">
                    <Check className="h-4 w-4" />{t("admin.approve")}
                  </button>
                  <button onClick={() => act(p.id, () => api.adminReject(p.id))} disabled={busyId === p.id}
                    className="inline-flex items-center gap-1 rounded-full border border-red-300 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:hover:bg-red-950">
                    <Trash2 className="h-4 w-4" />{t("admin.reject")}
                  </button>
                </div>
              </CardContent>
            </Card>
          </li>
        ))}
      </ul>
    );
  }

  // ---- Manage live providers tab ----
  function ManageTab() {
    const [list, setList] = useState<ManagedProvider[] | null>(null);
    const [editId, setEditId] = useState<string | null>(null);
    const [busyId, setBusyId] = useState<string | null>(null);

    const reload = () => api.adminListProviders().then(setList).catch(() => setList([]));
    useEffect(() => { reload(); }, []);

    const remove = async (id: string) => {
      if (!window.confirm(t("doc.confirmDelete"))) return;
      setBusyId(id);
      try { await api.adminReject(id); setList((l) => (l ?? []).filter((p) => p.id !== id)); }
      finally { setBusyId(null); }
    };

    const toggle = async (p: ManagedProvider) => {
      setBusyId(p.id);
      try {
        await api.adminSetActive(p.id, !p.isActive);
        setList((l) => (l ?? []).map((x) => (x.id === p.id ? { ...x, isActive: !x.isActive } : x)));
      } finally { setBusyId(null); }
    };

    if (list === null) return <p className="text-gray-500">{t("common.loading")}</p>;
    if (list.length === 0) return <p className="text-gray-500 dark:text-gray-400">{t("admin.noneLive")}</p>;

    return (
      <ul className="space-y-3">
        {list.map((p) =>
          editId === p.id ? (
            <li key={p.id}>
              <EditCard provider={p} onCancel={() => setEditId(null)} onSaved={() => { setEditId(null); reload(); }} />
            </li>
          ) : (
            <li key={p.id}>
              <Card className={p.isActive ? "" : "opacity-60"}>
                <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between">
                  <div className="space-y-1">
                    <p className="font-semibold">
                      {p.displayName}
                      <CategoryBadge category={p.category} t={t} />
                      {!p.isActive && <span className="ms-2 text-xs text-gray-400">({t("admin.hidden")})</span>}
                    </p>
                    <p className="text-sm text-gray-600 dark:text-gray-300">{loc(p.blurb, language)}</p>
                    <ContactLine p={p} />
                  </div>
                  <div className="flex shrink-0 flex-wrap gap-2">
                    <button onClick={() => setEditId(p.id)}
                      className="inline-flex items-center gap-1 rounded-full border border-gray-300 px-3 py-2 text-sm font-medium hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800">
                      <Pencil className="h-4 w-4" />{t("admin.edit")}
                    </button>
                    <button onClick={() => toggle(p)} disabled={busyId === p.id}
                      className="inline-flex items-center gap-1 rounded-full border border-gray-300 px-3 py-2 text-sm font-medium hover:bg-gray-50 disabled:opacity-50 dark:border-gray-700 dark:hover:bg-gray-800">
                      {p.isActive ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      {p.isActive ? t("admin.deactivate") : t("admin.activate")}
                    </button>
                    <button onClick={() => remove(p.id)} disabled={busyId === p.id}
                      className="inline-flex items-center gap-1 rounded-full border border-red-300 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:hover:bg-red-950">
                      <Trash2 className="h-4 w-4" />{t("admin.delete")}
                    </button>
                  </div>
                </CardContent>
              </Card>
            </li>
          )
        )}
      </ul>
    );
  }

  // ---- Leads tab ----
  function LeadsTab() {
    const [data, setData] = useState<LeadsOverview | null>(null);
    const now = new Date();
    const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 });
    useEffect(() => { api.adminLeads().then(setData).catch(() => setData({ summary: [], recent: [] })); }, []);

    const setStatus = async (lead: RecentLead, status: number) => {
      setData((d) => d && { ...d, recent: d.recent.map((r) => (r.id === lead.id ? { ...r, status } : r)) });
      try { await api.adminUpdateLeadStatus(lead.id, status); } catch { /* best-effort UI update */ }
    };

    if (data === null) return <p className="text-gray-500">{t("common.loading")}</p>;
    if (data.summary.length === 0) return <p className="text-gray-500 dark:text-gray-400">{t("leads.none")}</p>;

    return (
      <div className="space-y-6">
        {/* Per-provider counts — the invoice numbers. */}
        <Card>
          <CardContent className="py-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <h2 className="font-semibold">{t("leads.byProvider")}</h2>
              <label className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                {t("invoice.period")}
                <input
                  type="month"
                  value={`${period.year}-${String(period.month).padStart(2, "0")}`}
                  onChange={(e) => {
                    const [y, m] = e.target.value.split("-").map(Number);
                    if (y && m) setPeriod({ year: y, month: m });
                  }}
                  className="rounded-lg border border-gray-200 bg-white px-2 py-1 text-sm dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
                />
              </label>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-start text-gray-500 dark:text-gray-400">
                    <th className="py-1 text-start font-medium">{t("leads.provider")}</th>
                    <th className="py-1 text-end font-medium">{t("leads.thisMonth")}</th>
                    <th className="py-1 text-end font-medium">{t("leads.invoiceMonth")}</th>
                    <th className="py-1 text-end font-medium">{t("leads.converted")}</th>
                    <th className="py-1 text-end font-medium">{t("leads.billable")}</th>
                    <th className="py-1 text-end font-medium">{t("leads.helpfulRate")}</th>
                    <th className="py-1 text-end font-medium">{t("leads.total")}</th>
                    <th className="py-1 text-end font-medium">{t("invoice.generate")}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.summary.map((s) => (
                    <tr key={s.providerId} className="border-t border-gray-100 dark:border-gray-800">
                      <td className="py-2">
                        {s.displayName}
                        <span className="ms-2 text-xs text-gray-400">₪{s.pricePerLead}/{t("leads.perLead")}</span>
                      </td>
                      <td className="py-2 text-end font-semibold text-brand">{s.monthCount}</td>
                      <td className="py-2 text-end font-semibold">₪{s.monthAmount}</td>
                      <td className="py-2 text-end">{s.convertedMonthCount}</td>
                      <td className="py-2 text-end font-semibold text-brand">₪{s.billableMonthAmount}</td>
                      <td className="py-2 text-end">{s.helpfulRate == null ? "—" : `${Math.round(s.helpfulRate * 100)}%`}</td>
                      <td className="py-2 text-end">{s.totalCount}</td>
                      <td className="py-2 text-end">
                        <InvoiceCell
                          providerId={s.providerId} year={period.year} month={period.month}
                          hasEmail={!!s.contactEmail}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>

        {/* Recent individual leads. */}
        <Card>
          <CardContent className="py-4">
            <h2 className="mb-3 font-semibold">{t("leads.recent")}</h2>
            <ul className="divide-y divide-gray-100 dark:divide-gray-800">
              {data.recent.map((r) => (
                <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <span className="font-medium">
                    {r.displayName}
                    {r.ref && <span className="ms-2 rounded bg-gray-100 px-1.5 py-0.5 font-mono text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">#{r.ref}</span>}
                    {r.helpful != null && (
                      <ThumbsUp className={`ms-2 inline h-4 w-4 align-text-bottom ${r.helpful ? "text-brand" : "rotate-180 text-gray-400"}`} />
                    )}
                  </span>
                  <span className="flex flex-wrap items-center gap-2 text-gray-500 dark:text-gray-400">
                    <CategoryBadge category={r.category} t={t} />
                    <span>{t(URGENCY_KEYS[r.urgency] ?? "urg.low")}</span>
                    <select
                      aria-label={t("leads.status")}
                      value={r.status}
                      onChange={(e) => setStatus(r, Number(e.target.value))}
                      className="rounded-lg border border-gray-200 bg-white px-2 py-1 text-xs dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
                    >
                      {LEAD_STATUS_KEYS.map((k, i) => <option key={k} value={i}>{t(k)}</option>)}
                    </select>
                    <span>{new Date(r.createdAt).toLocaleString()}</span>
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
    );
  }

  // ---- Inline edit form ----
  function EditCard({ provider, onCancel, onSaved }: { provider: ManagedProvider; onCancel: () => void; onSaved: () => void }) {
    const [form, setForm] = useState({
      displayName: provider.displayName,
      category: provider.category,
      city: provider.city ?? "",
      phone: provider.phone ?? "",
      whatsApp: provider.whatsApp ?? "",
      contactEmail: provider.contactEmail ?? "",
      description: loc(provider.blurb, language),
      priority: provider.priority,
      pricePerLead: provider.pricePerLead,
    });
    const [busy, setBusy] = useState(false);
    const set = (k: keyof typeof form, v: string | number) => setForm((f) => ({ ...f, [k]: v }));

    const save = async () => {
      setBusy(true);
      try { await api.adminUpdateProvider(provider.id, { ...form, category: Number(form.category), priority: Number(form.priority), pricePerLead: Number(form.pricePerLead) }); onSaved(); }
      finally { setBusy(false); }
    };

    return (
      <Card>
        <CardContent className="space-y-3 py-4">
          <Input aria-label={t("partners.businessName")} placeholder={t("partners.businessName")} value={form.displayName} onChange={(e) => set("displayName", e.target.value)} />
          <select aria-label={t("partners.category")} value={form.category} onChange={(e) => set("category", Number(e.target.value))} className={inputCls}>
            {CATEGORY_KEYS.map((k, i) => <option key={k} value={i}>{t(k)}</option>)}
          </select>
          <textarea aria-label={t("partners.description")} value={form.description} onChange={(e) => set("description", e.target.value)} rows={2}
            className="w-full rounded-xl border border-gray-300 bg-white px-4 py-2 text-base focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100" />
          <div className="grid gap-3 sm:grid-cols-2">
            <Input aria-label={t("partners.city")} placeholder={t("partners.city")} value={form.city} onChange={(e) => set("city", e.target.value)} />
            <Input aria-label={t("partners.email")} placeholder={t("partners.email")} value={form.contactEmail} onChange={(e) => set("contactEmail", e.target.value)} />
            <Input aria-label={t("partners.phone")} placeholder={t("partners.phone")} value={form.phone} onChange={(e) => set("phone", e.target.value)} />
            <Input aria-label={t("partners.whatsapp")} placeholder={t("partners.whatsapp")} value={form.whatsApp} onChange={(e) => set("whatsApp", e.target.value)} />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Input type="number" aria-label={t("admin.priority")} placeholder={t("admin.priority")} value={form.priority} onChange={(e) => set("priority", e.target.value)} />
            <Input type="number" step="0.5" min="0" aria-label={t("admin.pricePerLead")} placeholder={t("admin.pricePerLead")} value={form.pricePerLead} onChange={(e) => set("pricePerLead", e.target.value)} />
          </div>
          <div className="flex gap-2">
            <Button onClick={save} disabled={busy} className="flex-1">{busy ? t("common.loading") : t("admin.save")}</Button>
            <button onClick={onCancel} className="inline-flex items-center gap-1 rounded-full border border-gray-300 px-4 text-sm font-medium hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800">
              <X className="h-4 w-4" />{t("admin.cancel")}
            </button>
          </div>
        </CardContent>
      </Card>
    );
  }
}
