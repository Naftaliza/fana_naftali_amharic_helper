"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Camera, Lock } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { incrementTrial, trialRemaining, TRIAL_LIMIT } from "@/lib/trial";
import type { AnalysisResult } from "@/lib/types";
import { AnalysisCard } from "@/components/AnalysisCard";
import { AnalyzingState } from "@/components/AnalyzingState";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

const ACCEPT = "image/*,application/pdf,.pdf,.jpg,.jpeg,.png";

/**
 * The core "upload a document and understand it" experience. Used as the home page (/)
 * and at /upload. Handles both the anonymous free-trial flow and the logged-in flow.
 */
export function UploadExperience() {
  const { t } = useLanguage();
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [remaining, setRemaining] = useState(TRIAL_LIMIT);
  const [trialResult, setTrialResult] = useState<AnalysisResult | null>(null);

  useEffect(() => { setRemaining(trialRemaining()); }, []);

  const handleFile = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      if (user) {
        const doc = await api.uploadDocument(file);
        router.push(`/documents/${doc.id}`);
      } else {
        const result = await api.trialAnalyze(file);
        incrementTrial();
        setRemaining(trialRemaining());
        setTrialResult(result);
        setBusy(false);
      }
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  };

  const outOfTries = !user && remaining <= 0;

  // While a document is being uploaded/analyzed, show the reassuring step indicator.
  if (busy) return <AnalyzingState />;

  if (!authLoading && outOfTries && !trialResult) {
    return (
      <div className="mx-auto flex max-w-md flex-col items-center gap-6 pt-16 text-center">
        <Lock className="h-14 w-14 text-brand" />
        <h1 className="text-2xl font-bold">{t("trial.overTitle")}</h1>
        <p className="text-lg text-gray-600">{t("trial.overBody")}</p>
        <div className="flex w-full flex-col gap-3">
          <Link href="/register"><Button size="lg" className="w-full">{t("nav.register")}</Button></Link>
          <Link href="/login"><Button variant="outline" size="lg" className="w-full">{t("nav.login")}</Button></Link>
        </div>
      </div>
    );
  }

  if (trialResult) {
    return (
      <div className="space-y-6">
        <AnalysisCard analysis={trialResult} trial />
        <Card className="border-brand bg-brand-light">
          <CardContent className="flex flex-col items-center gap-3 py-6 text-center">
            {!user && <p className="text-lg font-medium">{t("trial.savePrompt").replace("{n}", String(remaining))}</p>}
            <div className="flex flex-wrap justify-center gap-3">
              {!user && <Link href="/register"><Button>{t("nav.register")}</Button></Link>}
              <Button
                variant="outline"
                onClick={() => { setTrialResult(null); setError(null); }}
                disabled={!user && remaining <= 0}
              >
                {t("trial.tryAnother")}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-xl animate-fade-in-up flex-col items-center gap-6 pt-6 text-center">
      <h1 className="bg-brand-gradient bg-clip-text text-4xl font-bold leading-tight text-transparent">{t("landing.headline")}</h1>
      <p className="text-lg text-gray-600">{t("landing.subheadline")}</p>

      <button
        onClick={() => inputRef.current?.click()}
        className="mt-2 flex w-full flex-col items-center justify-center gap-4 rounded-3xl bg-brand-gradient px-8 py-14 text-white shadow-soft transition-transform hover:scale-[1.02] active:scale-100"
      >
        <Camera className="h-20 w-20" />
        <span className="text-2xl font-bold">{t("upload.bigButton")}</span>
      </button>

      <p className="text-sm text-gray-500">{t("upload.formats")}</p>
      {!user && (
        <p className="text-sm text-brand">{t("trial.remaining").replace("{n}", String(remaining))}</p>
      )}
      {error && <p className="text-red-600">{error}</p>}

      <input ref={inputRef} type="file" accept={ACCEPT} className="hidden"
        onChange={(e) => handleFile(e.target.files?.[0])} />
    </div>
  );
}
