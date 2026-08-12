import type { Language } from "@/lib/types";

function startOfDay(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

/**
 * A short "when" label for a recent past timestamp (today/yesterday/N days ago), falling back to
 * a plain locale date once "N days ago" stops being a useful at-a-glance unit. Used by
 * RecentDocuments — the only current caller of a *past* relative label (lib/deadlines.ts's
 * daysUntil is the future-facing equivalent, used by the deadline verdict band).
 */
export function relativeUploadLabel(isoDate: string, language: Language, t: (key: string) => string): string {
  const date = new Date(isoDate);
  const days = Math.round((startOfDay(new Date()) - startOfDay(date)) / 86_400_000);

  if (days <= 0) {
    const time = date.toLocaleTimeString(language, { hour: "2-digit", minute: "2-digit" });
    return t("recent.today").replace("{time}", time);
  }
  if (days === 1) return t("recent.yesterday");
  if (days < 7) return t("recent.daysAgo").replace("{n}", String(days));
  return date.toLocaleDateString(language);
}
