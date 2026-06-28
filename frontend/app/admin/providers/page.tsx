"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Check, Trash2, Mail, Phone, MessageCircle, MapPin } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { loc, type PendingProvider } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";

const CATEGORY_KEY = [
  "cat.government", "cat.bank", "cat.insurance", "cat.employment",
  "cat.healthcare", "cat.municipality", "cat.other",
];

export default function AdminProvidersPage() {
  const { t, language, rtl } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [pending, setPending] = useState<PendingProvider[] | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  // Guard: only admins. Wait for auth to resolve before deciding.
  useEffect(() => {
    if (loading) return;
    if (!user?.isAdmin) { router.replace("/"); return; }
    api.adminListPending().then(setPending).catch(() => setPending([]));
  }, [loading, user, router]);

  const act = async (id: string, fn: () => Promise<unknown>) => {
    setBusyId(id);
    try {
      await fn();
      setPending((list) => (list ?? []).filter((p) => p.id !== id));
    } finally {
      setBusyId(null);
    }
  };

  if (loading || !user?.isAdmin || pending === null) {
    return <p className="pt-16 text-center text-gray-500">{t("common.loading")}</p>;
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5 pt-6" dir={rtl ? "rtl" : "ltr"}>
      <h1 className="text-2xl font-bold">{t("admin.pendingTitle")}</h1>

      {pending.length === 0 ? (
        <p className="text-gray-500 dark:text-gray-400">{t("admin.empty")}</p>
      ) : (
        <ul className="space-y-3">
          {pending.map((p) => (
            <li key={p.id}>
              <Card>
                <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between">
                  <div className="space-y-1">
                    <p className="font-semibold">
                      {p.displayName}
                      <span className="ms-2 rounded-full bg-brand-light px-2 py-0.5 text-xs text-brand dark:bg-brand/15">
                        {t(CATEGORY_KEY[p.category] ?? "cat.other")}
                      </span>
                    </p>
                    <p className="text-sm text-gray-600 dark:text-gray-300">{loc(p.blurb, language)}</p>
                    <div className="flex flex-wrap gap-x-4 gap-y-1 pt-1 text-sm text-gray-500 dark:text-gray-400">
                      {p.city && <span className="inline-flex items-center gap-1"><MapPin className="h-4 w-4" />{p.city}</span>}
                      {p.contactEmail && <span className="inline-flex items-center gap-1"><Mail className="h-4 w-4" />{p.contactEmail}</span>}
                      {p.phone && <span className="inline-flex items-center gap-1"><Phone className="h-4 w-4" />{p.phone}</span>}
                      {p.whatsApp && <span className="inline-flex items-center gap-1"><MessageCircle className="h-4 w-4" />{p.whatsApp}</span>}
                    </div>
                  </div>
                  <div className="flex shrink-0 gap-2">
                    <button
                      onClick={() => act(p.id, () => api.adminApprove(p.id))}
                      disabled={busyId === p.id}
                      className="inline-flex items-center gap-1 rounded-full bg-brand px-3 py-2 text-sm font-medium text-white hover:bg-brand-dark disabled:opacity-50"
                    >
                      <Check className="h-4 w-4" />{t("admin.approve")}
                    </button>
                    <button
                      onClick={() => act(p.id, () => api.adminReject(p.id))}
                      disabled={busyId === p.id}
                      className="inline-flex items-center gap-1 rounded-full border border-red-300 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:hover:bg-red-950"
                    >
                      <Trash2 className="h-4 w-4" />{t("admin.reject")}
                    </button>
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
