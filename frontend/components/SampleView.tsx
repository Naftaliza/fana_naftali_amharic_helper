"use client";

import Link from "next/link";
import { Sparkles } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { SAMPLE_ANALYSIS } from "@/lib/sampleAnalysis";
import { sampleAudioSrcFor, samplePhraseAudioSrcFor } from "@/lib/sampleAudio";
import { AnalysisCard } from "@/components/AnalysisCard";
import { Button } from "@/components/ui/button";

/**
 * Renders a hand-written, fictional analysis through the real AnalysisCard — no API call, no
 * credits, no network. This is the demo's insurance policy: if Anthropic/Azure hiccup mid-pitch,
 * this page still works with Wi-Fi off (audio comes from public/audio/sample/, precached by the
 * service worker after one visit). See lib/sampleAnalysis.ts for the fixture.
 */
export function SampleView() {
  const { t, rtl } = useLanguage();

  return (
    <div className="mx-auto max-w-2xl space-y-4 pt-4">
      <div
        dir={rtl ? "rtl" : "ltr"}
        className="flex items-center gap-2 rounded-2xl border border-brand/30 bg-brand-light px-4 py-3 text-sm dark:border-brand/40 dark:bg-brand/15"
      >
        <Sparkles className="h-5 w-5 shrink-0 text-brand" aria-hidden="true" />
        <div>
          <p className="font-semibold text-brand">{t("sample.banner")}</p>
          <p className="text-gray-600 dark:text-gray-300">{t("sample.bannerBody")}</p>
        </div>
      </div>

      <AnalysisCard
        analysis={SAMPLE_ANALYSIS}
        trial
        audioSrcFor={sampleAudioSrcFor}
        phraseAudioSrcFor={samplePhraseAudioSrcFor}
      />

      <div className="flex justify-center pb-6 pt-2">
        <Link href="/">
          <Button>{t("sample.cta")}</Button>
        </Link>
      </div>
    </div>
  );
}
