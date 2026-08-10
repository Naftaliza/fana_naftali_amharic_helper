"use client";

import { useEffect, useRef, useState } from "react";
import { Loader2, Pause, Play } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { LANGUAGE_ENUM, SPOKEN_SECTION, type AnalysisResult, type Language, type SpokenSectionKey } from "@/lib/types";

/**
 * Small per-card play button — lets a low-literacy user hear just one passage (e.g. "just the
 * actions") instead of the whole document, all-or-nothing. Mirrors AnalysisCard's full-document
 * player but without a seek bar, since a single card's audio is short.
 */
export function SectionAudioButton({
  analysis,
  documentId,
  trial = false,
  section,
  label,
  audioSrcFor,
}: {
  analysis: AnalysisResult;
  documentId?: string;
  trial?: boolean;
  section: SpokenSectionKey;
  label: string;
  // See AnalysisCard's audioSrcFor doc. Only "Full" is guaranteed to exist for a static bundle
  // (e.g. /sample) — a null return here hides the button rather than showing a broken control.
  audioSrcFor?: (section: SpokenSectionKey, language: Language) => string | null;
}) {
  const { language } = useLanguage();
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [loading, setLoading] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [error, setError] = useState(false);
  const [hidden, setHidden] = useState(false);

  useEffect(() => {
    return () => {
      audioRef.current?.pause();
      if (audioRef.current?.src) URL.revokeObjectURL(audioRef.current.src);
    };
  }, []);

  // Discard any loaded audio when the language changes, same as AnalysisCard's full player —
  // otherwise the next press would keep playing the previous language.
  useEffect(() => {
    const a = audioRef.current;
    if (a) {
      a.pause();
      if (a.src) URL.revokeObjectURL(a.src);
      audioRef.current = null;
    }
    setPlaying(false);
    setError(false);
    // A different language may have a clip where this one didn't — give it another chance.
    setHidden(false);
  }, [language]);

  const toggle = async () => {
    const existing = audioRef.current;
    if (existing) {
      if (existing.paused) { existing.play(); setPlaying(true); }
      else { existing.pause(); setPlaying(false); }
      return;
    }
    setError(false);
    setLoading(true);
    try {
      let audio: HTMLAudioElement;
      if (audioSrcFor) {
        const src = audioSrcFor(section, language);
        if (!src) { setHidden(true); return; }
        audio = new Audio(src);
      } else {
        const blob = trial || !documentId
          ? await api.trialSpeech(analysis, LANGUAGE_ENUM[language], SPOKEN_SECTION[section])
          : await api.speech(documentId, LANGUAGE_ENUM[language], SPOKEN_SECTION[section]);
        audio = new Audio(URL.createObjectURL(blob));
      }
      audioRef.current = audio;
      audio.onended = () => setPlaying(false);
      audio.onerror = () => { setPlaying(false); if (audioSrcFor) setHidden(true); else setError(true); };
      await audio.play();
      setPlaying(true);
    } catch {
      if (audioSrcFor) setHidden(true); else setError(true);
    } finally {
      setLoading(false);
    }
  };

  if (hidden) return null;

  return (
    <button
      type="button"
      onClick={toggle}
      disabled={loading}
      aria-label={label}
      title={label}
      // h-11 w-11 (44px) — WCAG 2.5.5 target size; was h-8 w-8 (32px), the smaller of the two
      // undersized controls flagged on this screen (the other is the action checkbox).
      className={`grid h-11 w-11 shrink-0 place-items-center rounded-full hover:bg-gray-100 disabled:opacity-50 dark:hover:bg-gray-700 ${
        error ? "text-red-500" : "text-gray-400 hover:text-brand"
      }`}
    >
      {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : playing ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
    </button>
  );
}
