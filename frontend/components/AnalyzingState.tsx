"use client";

import { useEffect, useState } from "react";
import { Loader2, ScanText, Brain, Languages, Check } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { Card, CardContent } from "@/components/ui/card";

const STEP_KEYS = ["analyze.step1", "analyze.step2", "analyze.step3"] as const;
const ICONS = [ScanText, Brain, Languages];

/** Reassuring multi-step indicator shown during the ~30-60s analysis. */
export function AnalyzingState() {
  const { t } = useLanguage();
  const [step, setStep] = useState(0);

  // Advance through the steps on a timer (stops on the last one).
  useEffect(() => {
    const id = setInterval(() => setStep((s) => Math.min(s + 1, STEP_KEYS.length - 1)), 6000);
    return () => clearInterval(id);
  }, []);

  return (
    <Card className="animate-fade-in-up">
      <CardContent className="flex flex-col items-center gap-6 py-12 text-center">
        <Loader2 className="h-12 w-12 animate-spin text-brand" />
        <div className="w-full max-w-xs space-y-3">
          {STEP_KEYS.map((key, i) => {
            const Icon = ICONS[i];
            const done = i < step;
            const active = i === step;
            return (
              <div
                key={key}
                className={`flex items-center gap-3 rounded-xl px-4 py-2.5 text-start transition-colors ${
                  active ? "bg-brand-light" : ""
                }`}
              >
                <span className={`grid h-8 w-8 shrink-0 place-items-center rounded-full ${
                  done ? "bg-accent text-white" : active ? "bg-brand text-white" : "bg-gray-100 text-gray-400"
                }`}>
                  {done ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
                </span>
                <span className={active ? "font-medium text-gray-900" : done ? "text-gray-500" : "text-gray-400"}>
                  {t(key)}
                </span>
              </div>
            );
          })}
        </div>
        <p className="text-sm text-gray-500">{t("doc.analyzingHint")}</p>
      </CardContent>
    </Card>
  );
}
