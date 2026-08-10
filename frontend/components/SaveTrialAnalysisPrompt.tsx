"use client";

import { useEffect, useState } from "react";
import { FileCheck2, X } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { clearPendingTrialAnalysis, getPendingTrialAnalysis, type PendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

/**
 * Offers to save a trial analysis stashed just before the user tapped "register" (see
 * UploadExperience). Only makes sense once signed in — mirrors LeadFeedbackPrompt's global-
 * singleton placement in layout.tsx and its dismiss/backdrop styling.
 */
export function SaveTrialAnalysisPrompt() {
  const { user } = useAuth();
  const { t, rtl } = useLanguage();
  const [pending, setPending] = useState<PendingTrialAnalysis | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user) setPending(getPendingTrialAnalysis());
  }, [user]);

  if (!user || !pending) return null;

  const dismiss = () => {
    clearPendingTrialAnalysis();
    setPending(null);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.attachTrialAnalysis(pending.analysis);
      setSaved(true);
      clearPendingTrialAnalysis();
      window.setTimeout(() => setPending(null), 1500);
    } catch {
      setError(t("trial.attachError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      dir={rtl ? "rtl" : "ltr"}
      data-a11y-fixed
      className="pb-safe fixed inset-x-4 bottom-20 z-50 mx-auto max-w-sm sm:inset-x-auto sm:start-4 md:bottom-4"
    >
      <Card className="shadow-soft">
        <CardContent className="relative py-4">
          <button
            onClick={dismiss}
            aria-label={t("onboarding.skip")}
            className="absolute end-3 top-3 rounded-lg p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <X className="h-4 w-4" />
          </button>
          {saved ? (
            <p className="pe-6 text-sm font-medium text-brand">{t("trial.attachSaved")}</p>
          ) : (
            <>
              <p className="pe-6 text-sm font-medium">{t("trial.attachPrompt")}</p>
              {error && <p role="alert" className="mt-1 text-xs text-red-600">{error}</p>}
              <Button onClick={save} disabled={saving} className="mt-3 w-full">
                <FileCheck2 className="h-4 w-4" />{saving ? t("common.loading") : t("trial.attachSave")}
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
