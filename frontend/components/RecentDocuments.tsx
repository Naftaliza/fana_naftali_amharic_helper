"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { FileText, ChevronLeft, ChevronRight } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { DOCUMENT_STATUS, type DocumentSummary } from "@/lib/types";
import { relativeUploadLabel } from "@/lib/relativeTime";

const MAX_SHOWN = 3;

/**
 * A compact "last 3 uploads" list shown on the upload screen for a signed-in visitor, in place
 * of the marketing hero they don't need on a repeat visit (see UploadExperience). Self-gates on
 * auth and on having at least one document — renders nothing for an anonymous visitor or a
 * first-time user with zero uploads yet, rather than showing an empty state to explain away.
 * GET /api/documents already returns newest-first (ORDER BY UploadedAt DESC), so this is just
 * the first three.
 */
export function RecentDocuments() {
  const { user } = useAuth();
  const { t, language, rtl } = useLanguage();
  const [docs, setDocs] = useState<DocumentSummary[] | null>(null);

  useEffect(() => {
    if (!user) return;
    let cancelled = false;
    api.listDocuments()
      .then((list) => { if (!cancelled) setDocs(list); })
      .catch(() => { if (!cancelled) setDocs([]); }); // best-effort — not this page's main job
    return () => { cancelled = true; };
  }, [user]);

  if (!user || !docs || docs.length === 0) return null;

  const Chevron = rtl ? ChevronLeft : ChevronRight;

  return (
    <div className="w-full text-start" dir={rtl ? "rtl" : "ltr"}>
      <div className="mb-1.5 flex items-baseline justify-between">
        <h2 className="text-xs font-bold uppercase tracking-wide text-gray-500 dark:text-gray-400">
          {t("recent.title")}
        </h2>
        <Link href="/dashboard" className="text-sm font-medium text-brand hover:underline">
          {t("recent.viewAll")}
        </Link>
      </div>
      <div className="flex flex-col gap-1.5">
        {docs.slice(0, MAX_SHOWN).map((d) => {
          const pending = d.status === DOCUMENT_STATUS.Pending || d.status === DOCUMENT_STATUS.Processing;
          return (
            <Link
              key={d.id}
              href={`/documents/${d.id}`}
              className="flex items-center gap-2 rounded-xl border border-gray-100 bg-white px-2.5 py-1.5 dark:border-gray-800 dark:bg-gray-900"
            >
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-md bg-brand-light text-brand dark:bg-brand/20">
                <FileText className="h-3 w-3" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium text-gray-900 dark:text-gray-100">{d.fileName}</span>
                <span className="flex items-center gap-1 text-xs text-gray-400 dark:text-gray-500">
                  <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${pending ? "bg-amber-500" : "bg-green-500"}`} />
                  {relativeUploadLabel(d.uploadedAt, language, t)}
                </span>
              </span>
              <Chevron className="h-4 w-4 shrink-0 text-gray-300 dark:text-gray-600" />
            </Link>
          );
        })}
      </div>
    </div>
  );
}
