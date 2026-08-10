"use client";

import { AlertTriangle, Calendar, CheckSquare, FileText } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { isRtl, loc, urgencyIndexOf, type AnalysisResult } from "@/lib/types";
import { daysUntil } from "@/lib/deadlines";
import { Card, CardContent } from "@/components/ui/card";

const URGENCY_KEYS = ["urg.low", "urg.medium", "urg.high", "urg.critical"];
// Matches the --urgency-*-fg custom properties in globals.css (see the design-tokens section
// there) so this band and the pill inside AnalysisCard always agree, including under
// a11y-contrast and dark mode, without duplicating the actual color values here.
const URGENCY_VAR_BY_INDEX = ["--urgency-low-fg", "--urgency-medium-fg", "--urgency-high-fg", "--urgency-critical-fg"];

/**
 * The "verdict band" — answers documentType / urgency / nearest deadline / how many things you
 * must do, in that order, before any scrolling. Every value here already exists on
 * `AnalysisResult`; this is pure re-ranking of what AnalysisCard already renders further down as
 * five equal-weight cards, not a new data source. Rendered once, above the (now collapsible)
 * detail cards.
 */
export function AnalysisVerdict({ analysis }: { analysis: AnalysisResult }) {
  const { t, language } = useLanguage();
  const dir = isRtl(language) ? "rtl" : "ltr";
  const urgencyIndex = urgencyIndexOf(analysis.urgencyLevel);

  const mandatoryCount = analysis.requiredActions.filter((a) => a.isMandatory).length;

  const nearestDated = analysis.deadlines
    .filter((d): d is { date: string; description: typeof d.description } => !!d.date)
    .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())[0];

  const deadlineLabel = (() => {
    if (!nearestDated) return null;
    const n = daysUntil(nearestDated.date);
    const relative =
      n < 0 ? t("verdict.overdue") : n === 0 ? t("verdict.dueToday") : n === 1 ? t("verdict.dueTomorrow") : t("verdict.dueIn").replace("{n}", String(n));
    return `${new Date(nearestDated.date).toLocaleDateString(language)} · ${relative}`;
  })();

  return (
    <Card data-testid="analysis-verdict" className="border-2 animate-fade-in-up" style={{ borderColor: `var(${URGENCY_VAR_BY_INDEX[urgencyIndex]})` }}>
      <CardContent className="space-y-3 py-5" dir={dir}>
        <div className="flex items-center gap-2">
          <FileText className="h-5 w-5 shrink-0 text-gray-400" aria-hidden="true" />
          <p className="text-lg font-bold">{loc(analysis.documentType, language)}</p>
        </div>

        <div className="flex items-center gap-2 font-semibold" style={{ color: `var(${URGENCY_VAR_BY_INDEX[urgencyIndex]})` }}>
          <AlertTriangle className="h-5 w-5 shrink-0" aria-hidden="true" />
          <span>
            {t(URGENCY_KEYS[urgencyIndex] ?? "urg.low")}
            {urgencyIndex >= 2 ? ` — ${t("verdict.actionRequired")}` : ""}
          </span>
        </div>

        {deadlineLabel && (
          <div className="flex items-center gap-2 text-gray-700 dark:text-gray-300">
            <Calendar className="h-5 w-5 shrink-0 text-gray-400" aria-hidden="true" />
            <span>{deadlineLabel}</span>
          </div>
        )}

        {mandatoryCount > 0 && (
          <div className="flex items-center gap-2 text-gray-700 dark:text-gray-300">
            <CheckSquare className="h-5 w-5 shrink-0 text-gray-400" aria-hidden="true" />
            <span>{t("verdict.actionsCount").replace("{n}", String(mandatoryCount))}</span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
