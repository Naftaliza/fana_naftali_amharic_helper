"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ChevronDown } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { useTheme } from "@/lib/theme-context";
import type { EventDetail, Funnel } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";

// Fixed funnel order (not sorted by count) — registration always leads, analysis always trails,
// regardless of which event happens to have the highest count in a given window.
const STAGES: { event: string; labelKey: string }[] = [
  { event: "user_registered", labelKey: "analytics.registered" },
  { event: "email_verified", labelKey: "analytics.verified" },
  { event: "document_uploaded", labelKey: "analytics.uploaded" },
  { event: "document_analyzed", labelKey: "analytics.analyzed" },
];

const BAR_COLOR = { light: "#2a78d6", dark: "#3987e5" };

export default function AdminAnalyticsPage() {
  const { t, rtl, language } = useLanguage();
  const { user, loading } = useAuth();
  const { theme } = useTheme();
  const router = useRouter();
  const [days, setDays] = useState(30);
  const [funnel, setFunnel] = useState<Funnel | null>(null);
  const [openStage, setOpenStage] = useState<string | null>(null);
  // Per-event detail cache, keyed by event name — cleared whenever the period changes since
  // the list is period-scoped too, not just the count.
  const [details, setDetails] = useState<Record<string, EventDetail[] | "loading" | "error">>({});

  useEffect(() => {
    if (loading) return;
    if (!user?.isAdmin) router.replace("/");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user?.isAdmin) return;
    setFunnel(null);
    setOpenStage(null);
    setDetails({});
    api.adminFunnel(days).then(setFunnel).catch(() => setFunnel({ sinceUtc: new Date().toISOString(), counts: [] }));
  }, [user, days]);

  if (loading || !user?.isAdmin) {
    return <p className="pt-16 text-center text-gray-500">{t("common.loading")}</p>;
  }

  const countOf = (event: string) => funnel?.counts.find((c) => c.name === event)?.count ?? 0;
  const max = Math.max(...STAGES.map((s) => countOf(s.event)), 1);
  const hasData = funnel !== null && funnel.counts.some((c) => c.count > 0);
  const color = BAR_COLOR[theme];

  const toggleStage = (event: string) => {
    if (openStage === event) {
      setOpenStage(null);
      return;
    }
    setOpenStage(event);
    if (!details[event]) {
      setDetails((d) => ({ ...d, [event]: "loading" }));
      api.adminFunnelEvents(event, days)
        .then((list) => setDetails((d) => ({ ...d, [event]: list })))
        .catch(() => setDetails((d) => ({ ...d, [event]: "error" })));
    }
  };

  const periodBtn = (value: number, label: string) => (
    <button
      key={value}
      onClick={() => setDays(value)}
      className={`rounded-full px-4 py-2 text-sm font-medium ${
        days === value ? "bg-brand text-white" : "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"
      }`}
    >
      {label}
    </button>
  );

  return (
    <div className="mx-auto max-w-3xl space-y-5 pt-6" dir={rtl ? "rtl" : "ltr"}>
      <h1 className="text-2xl font-bold">{t("analytics.title")}</h1>

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-gray-500 dark:text-gray-400">{t("analytics.period")}</span>
        {periodBtn(7, t("analytics.days7"))}
        {periodBtn(30, t("analytics.days30"))}
        {periodBtn(90, t("analytics.days90"))}
      </div>

      <Card>
        <CardContent className="space-y-4 py-4">
          {funnel === null ? (
            <p className="text-gray-500">{t("common.loading")}</p>
          ) : !hasData ? (
            <p className="text-gray-500 dark:text-gray-400">{t("analytics.empty")}</p>
          ) : (
            <>
              <p className="text-xs text-gray-400 dark:text-gray-500">
                {t("analytics.since")} {new Date(funnel.sinceUtc).toLocaleDateString()}
              </p>
              <div className="space-y-1">
                {STAGES.map((s) => {
                  const count = countOf(s.event);
                  const isOpen = openStage === s.event;
                  const rows = details[s.event];
                  return (
                    <div key={s.event}>
                      <button
                        onClick={() => toggleStage(s.event)}
                        disabled={count === 0}
                        className="flex w-full items-center gap-2.5 rounded-lg py-1.5 text-start transition-colors hover:bg-gray-50 disabled:cursor-default disabled:hover:bg-transparent dark:hover:bg-gray-800/60"
                      >
                        <span className="w-32 shrink-0 truncate text-sm text-gray-600 dark:text-gray-300">
                          {t(s.labelKey)}
                        </span>
                        <span className="h-3 flex-1 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
                          <span
                            className="block h-full rounded-full transition-[width]"
                            style={{ width: `${(count / max) * 100}%`, background: color }}
                          />
                        </span>
                        <span className="w-10 shrink-0 text-end text-sm font-semibold tabular-nums">{count}</span>
                        {count > 0 && (
                          <ChevronDown
                            className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${isOpen ? "rotate-180" : ""}`}
                          />
                        )}
                      </button>

                      {isOpen && (
                        <div className="ms-2 mb-2 space-y-1 border-s-2 border-gray-100 ps-4 dark:border-gray-800">
                          {rows === "loading" || rows === undefined ? (
                            <p className="py-2 text-sm text-gray-400">{t("common.loading")}</p>
                          ) : rows === "error" ? (
                            <p className="py-2 text-sm text-red-500">{t("analytics.loadError")}</p>
                          ) : rows.length === 0 ? (
                            <p className="py-2 text-sm text-gray-400">{t("analytics.noEvents")}</p>
                          ) : (
                            <ul className="max-h-64 space-y-1 overflow-y-auto py-1">
                              {rows.map((r, i) => (
                                <li
                                  key={i}
                                  className="flex flex-wrap items-center justify-between gap-2 text-sm text-gray-600 dark:text-gray-300"
                                >
                                  <span className={r.email ? "" : "italic text-gray-400"}>
                                    {r.email ?? t("analytics.deletedAccount")}
                                  </span>
                                  <span className="text-xs text-gray-400 dark:text-gray-500">
                                    {new Date(r.createdAt).toLocaleString(language === "en" ? "en-US" : undefined)}
                                  </span>
                                </li>
                              ))}
                            </ul>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
