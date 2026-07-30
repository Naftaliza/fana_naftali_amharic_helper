"use client";

import Link from "next/link";
import { CalendarClock } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { loc, type DocumentSummary } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const MAX_SHOWN = 5;

/**
 * Flattens every document's deadlines into one nearest-first list, dropping anything already
 * past. Falls out of the DocumentSummary.deadlines DTO addition (see backend ListDocumentsHandler)
 * so no extra network round trip is needed beyond the dashboard's existing listDocuments() call.
 */
export function ComingUpStrip({ docs }: { docs: DocumentSummary[] }) {
  const { t, language, rtl } = useLanguage();
  const now = Date.now();

  const upcoming = docs
    .flatMap((d) => d.deadlines.map((dl) => ({ doc: d, deadline: dl })))
    .filter((x) => x.deadline.date && new Date(x.deadline.date).getTime() >= now)
    .sort((a, b) => new Date(a.deadline.date!).getTime() - new Date(b.deadline.date!).getTime())
    .slice(0, MAX_SHOWN);

  if (upcoming.length === 0) return null;

  return (
    <Card className="border-brand bg-brand-light dark:bg-brand/15">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg"><CalendarClock className="h-5 w-5 text-brand" />{t("dashboard.comingUp")}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="space-y-2" dir={rtl ? "rtl" : "ltr"}>
          {upcoming.map(({ doc, deadline }, i) => (
            <li key={`${doc.id}-${i}`}>
              <Link href={`/documents/${doc.id}`} className="flex flex-wrap items-center gap-2 hover:underline">
                <strong>{new Date(deadline.date!).toLocaleDateString(language)}</strong>
                <span>{loc(deadline.description, language)}</span>
                <span className="text-sm text-gray-500 dark:text-gray-400">— {doc.fileName}</span>
              </Link>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
