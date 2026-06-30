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
import { CameraCapture } from "@/components/CameraCapture";
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
  const [cameraOpen, setCameraOpen] = useState(false);

  useEffect(() => { setRemaining(trialRemaining()); }, []);

  const handleFiles = async (files: File[]) => {
    if (!files.length) return;
    // A PDF is already multi-page and is OCR'd as a single block — don't let it be mixed with
    // image pages in one document (it would break page numbering). A lone PDF is fine.
    if (files.length > 1 && files.some((f) => f.type === "application/pdf")) {
      setError(t("upload.noMixedPdf"));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      if (user) {
        const doc = await api.uploadDocument(files);
        const q = doc.skippedPages > 0 ? `?skipped=${doc.skippedPages}` : "";
        router.push(`/documents/${doc.id}${q}`);
      } else {
        const result = await api.trialAnalyze(files);
        incrementTrial();
        setRemaining(trialRemaining());
        setTrialResult(result);
        setBusy(false);
      }
    } catch (err) {
      const msg = (err as Error).message;
      setError(msg === "UPLOAD_TOO_LARGE" ? t("upload.tooLarge") : msg);
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
        <p className="text-lg text-gray-600 dark:text-gray-400">{t("trial.overBody")}</p>
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
        <Card className="border-brand bg-brand-light dark:bg-brand/15">
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
      <p className="text-lg text-gray-600 dark:text-gray-400">{t("landing.subheadline")}</p>

      <button
        onClick={() => setCameraOpen(true)}
        className="mt-2 flex w-full flex-col items-center justify-center gap-4 rounded-3xl bg-brand-gradient px-8 py-14 text-white shadow-soft transition-transform hover:scale-[1.02] active:scale-100"
      >
        <Camera className="h-20 w-20" />
        <span className="text-2xl font-bold">{t("upload.takePhoto")}</span>
      </button>

      {/* Secondary: upload an existing file / PDF. */}
      <button onClick={() => inputRef.current?.click()} className="text-base font-medium text-brand underline-offset-4 hover:underline">
        {t("upload.orChooseFile")}
      </button>

      <p className="text-sm text-gray-500 dark:text-gray-400">{t("upload.formats")}</p>
      {!user && (
        <p className="text-sm text-brand">{t("trial.remaining").replace("{n}", String(remaining))}</p>
      )}
      {error && <p role="alert" className="text-red-600">{error}</p>}

      <input ref={inputRef} type="file" accept={ACCEPT} multiple className="hidden"
        onChange={(e) => handleFiles(Array.from(e.target.files ?? []))} />

      {cameraOpen && (
        <CameraCapture
          onClose={() => setCameraOpen(false)}
          onChooseFile={() => inputRef.current?.click()}
          onCapture={(files) => { setCameraOpen(false); handleFiles(files); }}
        />
      )}
    </div>
  );
}
