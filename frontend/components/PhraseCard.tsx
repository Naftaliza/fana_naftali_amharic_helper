"use client";

import { useState } from "react";
import { Check, Copy, Phone, Volume2 } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { isRtl, loc, type HebrewPhrase } from "@/lib/types";

/**
 * The exact Hebrew sentence to say at a counter or on the phone, with its meaning explained in
 * the reader's own language and a play button that speaks it with the Hebrew voice — regardless
 * of the current UI language, since the sentence IS Hebrew (see HebrewPhrase in lib/types.ts).
 *
 * Demo slice of feature #4: currently only /sample's fixture carries a hebrewPhrase, hand-
 * authored rather than generated. The full version — the AI producing one of these for every
 * real document's required actions — is deferred; it touches the analysis tool schema and the
 * measured 97% clean-parse rate, which isn't something to risk in the days before a demo. See
 * the implementation plan for the full design (a widened RequiredAction record, a TtsAudioCache
 * migration for the phrase's own cache slot, and a prompt-only, no-skeleton schema addition).
 */
export function PhraseCard({
  phrase,
  contactPhone,
  audioSrc,
}: {
  phrase: HebrewPhrase;
  contactPhone?: string | null;
  audioSrc?: string | null;
}) {
  const { t, language } = useLanguage();
  const [copied, setCopied] = useState(false);
  const [playing, setPlaying] = useState(false);

  const copy = async () => {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(phrase.say);
      } else {
        // Fallback for older Android WebViews without the async Clipboard API.
        const el = document.createElement("textarea");
        el.value = phrase.say;
        el.style.position = "fixed";
        el.style.opacity = "0";
        document.body.appendChild(el);
        el.select();
        document.execCommand("copy");
        document.body.removeChild(el);
      }
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard denied — nothing to recover; the text is still visible to copy by hand.
    }
  };

  const play = () => {
    if (!audioSrc) return;
    const audio = new Audio(audioSrc);
    audio.onended = () => setPlaying(false);
    audio.onerror = () => setPlaying(false);
    setPlaying(true);
    audio.play().catch(() => setPlaying(false));
  };

  return (
    <div className="mt-2 rounded-2xl border border-brand/20 bg-brand-light p-3 dark:border-brand/30 dark:bg-brand/10">
      <p className="mb-1 text-xs font-semibold text-brand">{t("phrase.sayTitle")}</p>
      <div className="flex items-start justify-between gap-2">
        <p dir="rtl" lang="he" className="flex-1 text-lg font-medium text-gray-900 dark:text-gray-100">
          {phrase.say}
        </p>
        {audioSrc && (
          <button
            onClick={play}
            disabled={playing}
            aria-label={t("phrase.play")}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-brand hover:bg-white/60 disabled:opacity-50 dark:hover:bg-black/20"
          >
            <Volume2 className="h-4 w-4" />
          </button>
        )}
      </div>

      <p className="mt-2 text-xs font-semibold text-gray-500 dark:text-gray-400">{t("phrase.meaningTitle")}</p>
      <p dir={isRtl(language) ? "rtl" : "ltr"} className="text-sm text-gray-700 dark:text-gray-300">
        {loc(phrase.meaning, language)}
      </p>

      <div className="mt-3 flex flex-wrap gap-2">
        <button
          onClick={copy}
          className="flex items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-2.5 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-200 dark:hover:bg-gray-800"
        >
          {copied ? <Check className="h-3.5 w-3.5 text-green-600" /> : <Copy className="h-3.5 w-3.5" />}
          {copied ? t("phrase.copied") : t("phrase.copy")}
        </button>
        {contactPhone && (
          <a
            href={`tel:${contactPhone}`}
            className="flex items-center gap-1.5 rounded-lg bg-brand px-2.5 py-1.5 text-xs font-medium text-white hover:bg-brand-dark"
          >
            <Phone className="h-3.5 w-3.5" />
            {t("phrase.call")}
          </a>
        )}
      </div>
    </div>
  );
}
