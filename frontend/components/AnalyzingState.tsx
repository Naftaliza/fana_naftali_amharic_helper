"use client";

import { useEffect, useState } from "react";
import { Loader2, ScanText, Brain, Languages, Check } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { Card, CardContent } from "@/components/ui/card";

const STEP_KEYS = ["analyze.step1", "analyze.step2", "analyze.step3"] as const;
const ICONS = [ScanText, Brain, Languages];

/**
 * Waiting state shown during document processing.
 *
 * mode="ocr": driven by real backend progress (processedPages/totalPages from DocumentProcessor,
 * polled via GET /documents/{id}) — a genuine progress bar instead of a guess.
 *
 * mode="analyze" (default): the single Claude analysis call has no page-level signal to report,
 * so this falls back to a step checklist plus a live elapsed-time counter — it used to freeze
 * once the fake per-step timer ran out (steps completed at a fixed 12s regardless of the actual
 * 30-60s call), leaving the UI looking stuck; the counter keeps it honestly "still working".
 */
export function AnalyzingState({
  mode = "analyze",
  processedPages,
  totalPages,
}: {
  mode?: "ocr" | "analyze";
  processedPages?: number;
  totalPages?: number;
}) {
  const { t } = useLanguage();
  const [step, setStep] = useState(0);
  const [elapsed, setElapsed] = useState(0);

  useEffect(() => {
    if (mode !== "analyze") return;
    const stepId = setInterval(() => setStep((s) => Math.min(s + 1, STEP_KEYS.length - 1)), 6000);
    const elapsedId = setInterval(() => setElapsed((e) => e + 1), 1000);
    return () => {
      clearInterval(stepId);
      clearInterval(elapsedId);
    };
  }, [mode]);

  if (mode === "ocr") {
    const total = totalPages ?? 0;
    const done = processedPages ?? 0;
    const pct = total > 0 ? Math.round((Math.min(done, total) / total) * 100) : 0;
    return (
      <Card className="animate-fade-in-up" role="status" aria-live="polite">
        <CardContent className="flex flex-col items-center gap-6 py-12 text-center">
          <Loader2 className="h-12 w-12 animate-spin text-brand" aria-hidden="true" />
          <div className="w-full max-w-xs space-y-2">
            <div className="h-2.5 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
              <div className="h-full rounded-full bg-brand transition-all duration-500" style={{ width: `${pct}%` }} />
            </div>
            <p className="text-sm font-medium text-gray-700 dark:text-gray-300">
              {total > 0
                ? t("doc.ocrProgress")
                    .replace("{current}", String(Math.min(done + 1, total)))
                    .replace("{total}", String(total))
                : t("doc.analyzing")}
            </p>
          </div>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("doc.analyzingHint")}</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="animate-fade-in-up" role="status" aria-live="polite">
      <CardContent className="flex flex-col items-center gap-6 py-12 text-center">
        <Loader2 className="h-12 w-12 animate-spin text-brand" aria-hidden="true" />
        <div className="w-full max-w-xs space-y-3">
          {STEP_KEYS.map((key, i) => {
            const Icon = ICONS[i];
            const done = i < step;
            const active = i === step;
            return (
              <div
                key={key}
                className={`flex items-center gap-3 rounded-xl px-4 py-2.5 text-start transition-colors ${
                  active ? "bg-brand-light dark:bg-brand/20" : ""
                }`}
              >
                <span className={`grid h-8 w-8 shrink-0 place-items-center rounded-full ${
                  done ? "bg-accent text-white" : active ? "bg-brand text-white" : "bg-gray-100 text-gray-400 dark:bg-gray-800 dark:text-gray-500"
                }`}>
                  {done ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
                </span>
                <span className={active ? "font-medium text-gray-900 dark:text-gray-100" : done ? "text-gray-500 dark:text-gray-400" : "text-gray-400 dark:text-gray-500"}>
                  {t(key)}
                </span>
              </div>
            );
          })}
        </div>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          {t("doc.analyzingHint")}
          {elapsed > 0 ? ` ${t("doc.elapsedSeconds").replace("{n}", String(elapsed))}` : ""}
        </p>
      </CardContent>
    </Card>
  );
}
