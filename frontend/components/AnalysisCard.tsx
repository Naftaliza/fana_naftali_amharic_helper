"use client";

import { useEffect, useRef, useState } from "react";
import { AlertTriangle, CheckSquare, Calendar, ListChecks, Volume2, Loader2, Play, Pause, RotateCcw } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { LANGUAGE_ENUM, isRtl, loc, type AnalysisResult, type UrgencyLevel } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ReferralBlock } from "@/components/ReferralBlock";

const urgencyColor: Record<UrgencyLevel, string> = {
  Low: "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300",
  Medium: "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/40 dark:text-yellow-300",
  High: "bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-300",
  Critical: "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300",
};

export function AnalysisCard({
  analysis,
  documentId,
  trial = false,
}: {
  analysis: AnalysisResult;
  documentId?: string;
  trial?: boolean;
}) {
  const { t, language } = useLanguage();
  // Analysis content is shown in the chosen language; direction follows that language.
  const dir = isRtl(language) ? "rtl" : "ltr";
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [loading, setLoading] = useState(false);
  const [ready, setReady] = useState(false);   // audio fetched & loaded
  const [playing, setPlaying] = useState(false);
  const [duration, setDuration] = useState(0);
  const [current, setCurrent] = useState(0);
  const [error, setError] = useState<string | null>(null);

  // Stop and release any audio when the component unmounts.
  useEffect(() => {
    return () => {
      audioRef.current?.pause();
      if (audioRef.current?.src) URL.revokeObjectURL(audioRef.current.src);
    };
  }, []);

  // The audio is generated for one language. When the user switches language,
  // discard it and reset to the "Listen" button so the next play re-fetches in
  // the new language (otherwise it keeps playing the previous language).
  useEffect(() => {
    const a = audioRef.current;
    if (a) {
      a.pause();
      if (a.src) URL.revokeObjectURL(a.src);
      audioRef.current = null;
    }
    setReady(false);
    setPlaying(false);
    setDuration(0);
    setCurrent(0);
    setError(null);
  }, [language]);

  // First click: fetch the audio, wire up the player, and start playing.
  const start = async () => {
    setError(null);
    setLoading(true);
    try {
      const blob = trial || !documentId
        ? await api.trialSpeech(analysis, LANGUAGE_ENUM[language])
        : await api.speech(documentId, LANGUAGE_ENUM[language]);
      const audio = new Audio(URL.createObjectURL(blob));
      audioRef.current = audio;
      audio.onloadedmetadata = () => setDuration(audio.duration || 0);
      audio.ontimeupdate = () => setCurrent(audio.currentTime);
      audio.onended = () => setPlaying(false);
      audio.onerror = () => { setPlaying(false); setError(t("doc.speechError")); };
      setReady(true);
      await audio.play();
      setPlaying(true);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  const togglePlay = () => {
    const a = audioRef.current;
    if (!a) return;
    if (a.paused) { a.play(); setPlaying(true); }
    else { a.pause(); setPlaying(false); }
  };

  const skip = (delta: number) => {
    const a = audioRef.current;
    if (!a) return;
    a.currentTime = Math.min(Math.max(a.currentTime + delta, 0), duration || a.duration || 0);
    setCurrent(a.currentTime);
  };

  const seek = (value: number) => {
    const a = audioRef.current;
    if (!a) return;
    a.currentTime = value;
    setCurrent(value);
  };

  const fmt = (s: number) => {
    if (!Number.isFinite(s)) return "0:00";
    const m = Math.floor(s / 60);
    const sec = Math.floor(s % 60);
    return `${m}:${sec.toString().padStart(2, "0")}`;
  };

  return (
    <div className="grid animate-fade-in-up gap-4">
      {/* Sticky so the audio control stays reachable while scrolling the result. */}
      <div className="sticky top-20 z-30 flex flex-col gap-1">
        {!ready ? (
          <Button onClick={start} disabled={loading} className="self-start shadow-soft">
            {loading ? <Loader2 className="h-5 w-5 animate-spin" /> : <Volume2 className="h-5 w-5" />}
            {loading ? t("common.loading") : t("doc.listen")}
          </Button>
        ) : (
          // Compact, subtle player: rewind 5s · play/pause · seek bar · time.
          <div className="flex w-full max-w-md items-center gap-2 rounded-full border border-gray-100 bg-white/95 px-3 py-1.5 shadow-soft backdrop-blur dark:border-gray-700 dark:bg-gray-800/95">
            <button
              onClick={() => skip(-5)}
              aria-label={t("doc.back5")}
              className="relative grid h-8 w-8 shrink-0 place-items-center rounded-full text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              <RotateCcw className="h-5 w-5" />
              <span className="absolute text-[8px] font-bold">5</span>
            </button>
            <button
              onClick={togglePlay}
              aria-label={playing ? t("doc.pause") : t("doc.play")}
              className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-brand text-white hover:bg-brand-dark"
            >
              {playing ? <Pause className="h-5 w-5" /> : <Play className="h-5 w-5 ps-0.5" />}
            </button>
            <input
              type="range"
              min={0}
              max={duration || 0}
              step={0.1}
              value={current}
              onChange={(e) => seek(Number(e.target.value))}
              aria-label={t("doc.seek")}
              className="h-1.5 flex-1 cursor-pointer accent-brand"
            />
            <span className="shrink-0 text-xs tabular-nums text-gray-500 dark:text-gray-400">{fmt(current)} / {fmt(duration)}</span>
          </div>
        )}
        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      </div>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>{t("doc.summary")}</CardTitle>
          <span className={`rounded-full px-3 py-1 text-sm font-medium ${urgencyColor[analysis.urgencyLevel]}`}>
            {t("doc.urgency")}: {analysis.urgencyLevel}
          </span>
        </CardHeader>
        <CardContent>
          <p className="text-gray-700 dark:text-gray-300" dir={dir}>{loc(analysis.summary, language)}</p>
          <p className="mt-2 text-sm text-gray-500 dark:text-gray-400" dir={dir}>{t("doc.type")}: {loc(analysis.documentType, language)}</p>
        </CardContent>
      </Card>

      {/* Sponsored referrals — placed high (right under the summary) so users see the offer
          to get help while the document's urgency is fresh. Renders nothing when none match. */}
      <ReferralBlock analysis={analysis} documentId={documentId} />

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2 text-lg"><ListChecks className="h-5 w-5 text-brand" />{t("doc.keyPoints")}</CardTitle></CardHeader>
          <CardContent>
            <ul className="list-inside list-disc space-y-1 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.keyPoints.map((p, i) => <li key={i}>{loc(p, language)}</li>)}
            </ul>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2 text-lg"><CheckSquare className="h-5 w-5 text-brand" />{t("doc.actions")}</CardTitle></CardHeader>
          <CardContent>
            <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.requiredActions.map((a, i) => (
                <li key={i} className="flex items-start gap-2">
                  {a.isMandatory && <AlertTriangle className="mt-1 h-4 w-4 shrink-0 text-orange-500" />}
                  <span>{loc(a.description, language)}</span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2 text-lg"><Calendar className="h-5 w-5 text-brand" />{t("doc.deadlines")}</CardTitle></CardHeader>
        <CardContent>
          {analysis.deadlines.length === 0 ? (
            <p className="text-gray-500 dark:text-gray-400">—</p>
          ) : (
            <ul className="space-y-1 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.deadlines.map((d, i) => (
                <li key={i}>
                  {d.date ? <strong>{new Date(d.date).toLocaleDateString()}</strong> : null} {loc(d.description, language)}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-lg">{t("doc.explanation")}</CardTitle></CardHeader>
        <CardContent><p className="whitespace-pre-wrap text-gray-700 dark:text-gray-300" dir={dir}>{loc(analysis.explanation, language)}</p></CardContent>
      </Card>
    </div>
  );
}
