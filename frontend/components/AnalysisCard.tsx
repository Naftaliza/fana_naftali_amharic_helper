"use client";

import { useEffect, useRef, useState } from "react";
import { AlertTriangle, CalendarPlus, ChevronDown, CheckSquare, Calendar, ListChecks, Volume2, Loader2, Play, Pause, RotateCcw } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { LANGUAGE_ENUM, urgencyIndexOf, isRtl, loc, type AnalysisResult, type Language, type SpokenSectionKey } from "@/lib/types";
import { daysUntil } from "@/lib/deadlines";
import { getSectionOverride, setSectionOverride, type CollapsibleSection } from "@/lib/sectionExpand";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { AnalysisVerdict } from "@/components/AnalysisVerdict";
import { ReferralBlock } from "@/components/ReferralBlock";
import { SectionAudioButton } from "@/components/SectionAudioButton";
import { isActionChecked, setActionChecked } from "@/lib/actionProgress";
import { buildDeadlineIcs } from "@/lib/ics";
import { markAnalysisSeen } from "@/lib/analysisSeen";
import { PhraseCard } from "@/components/PhraseCard";

const URGENCY_KEYS = ["urg.low", "urg.medium", "urg.high", "urg.critical"];
// Matches the --urgency-*-fg/bg custom properties in globals.css — see the design-tokens
// section there for why this reads a CSS variable instead of a literal Tailwind color class:
// a11y-contrast redefines these once, centrally, instead of needing its own selector for every
// place a color like this is used (which is exactly how the old bg-green-100/text-green-800-style
// classes here went untouched by high-contrast mode before).
const URGENCY_VAR_BY_INDEX = ["low", "medium", "high", "critical"];

/**
 * A collapsed-by-default section (Key points / Actions / Deadlines) that expands on tap, or
 * automatically when `autoExpand` says its content is high-stakes (a mandatory action, a near
 * deadline) — see the two call sites below for the actual rules. A manual toggle always wins
 * over the auto rule afterward, persisted per document via lib/sectionExpand.ts.
 */
function useSectionExpanded(documentId: string | undefined, section: CollapsibleSection, autoExpand: boolean) {
  const [expanded, setExpanded] = useState(autoExpand);

  useEffect(() => {
    const override = documentId ? getSectionOverride(documentId, section) : undefined;
    setExpanded(override ?? autoExpand);
    // Only re-run when the document/section identity changes or the auto rule's own inputs
    // change — not on every render, or a manual collapse would immediately get overwritten.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [documentId, section, autoExpand]);

  const toggle = () => {
    setExpanded((prev) => {
      const next = !prev;
      if (documentId) setSectionOverride(documentId, section, next);
      return next;
    });
  };

  return [expanded, toggle] as const;
}

function SectionToggleHeader({
  icon: Icon,
  title,
  count,
  expanded,
  onToggle,
  audioButton,
}: {
  icon: typeof ListChecks;
  title: string;
  count: number;
  expanded: boolean;
  onToggle: () => void;
  audioButton: React.ReactNode;
}) {
  const { t } = useLanguage();
  return (
    <CardHeader className="flex flex-row items-center justify-between">
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={expanded}
        aria-label={`${title} · ${count} — ${expanded ? t("doc.collapse") : t("doc.expand")}`}
        className="flex min-h-11 flex-1 items-center gap-2 text-start"
      >
        <CardTitle className="flex items-center gap-2 text-lg">
          <Icon className="h-5 w-5 text-brand" aria-hidden="true" />
          {title} <span className="text-base font-normal text-gray-400">· {count}</span>
        </CardTitle>
        <ChevronDown aria-hidden="true" className={`h-5 w-5 shrink-0 text-gray-400 transition-transform ${expanded ? "rotate-180" : ""}`} />
      </button>
      {audioButton}
    </CardHeader>
  );
}

export function AnalysisCard({
  analysis,
  documentId,
  trial = false,
  audioSrcFor,
  phraseAudioSrcFor,
}: {
  analysis: AnalysisResult;
  documentId?: string;
  trial?: boolean;
  // When provided, audio plays from a static file (public/audio/...) instead of hitting
  // api.speech/api.trialSpeech — used by /sample so the demo works fully offline, no credits,
  // no network. A null return means no clip was generated for that section; the caller handles
  // the miss (the main player surfaces doc.speechError, SectionAudioButton hides itself).
  audioSrcFor?: (section: SpokenSectionKey, language: Language) => string | null;
  // Static audio for a required action's Hebrew phrase card, keyed by action index. Always the
  // Hebrew voice — see HebrewPhrase in lib/types.ts. No real-analysis backend route exists for
  // this yet (feature #4 is a hand-authored /sample-only slice); real analyses simply won't
  // have `hebrewPhrase` set, so PhraseCard never renders for them.
  phraseAudioSrcFor?: (actionIndex: number) => string | null;
}) {
  const { t, language } = useLanguage();
  // Analysis content is shown in the chosen language; direction follows that language.
  const dir = isRtl(language) ? "rtl" : "ltr";
  const urgencyIndex = urgencyIndexOf(analysis.urgencyLevel);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [loading, setLoading] = useState(false);
  const [ready, setReady] = useState(false);   // audio fetched & loaded
  const [playing, setPlaying] = useState(false);
  const [duration, setDuration] = useState(0);
  const [current, setCurrent] = useState(0);
  const [error, setError] = useState<string | null>(null);
  // isActionChecked reads localStorage directly (not React state) so it survives remounts across
  // page navigations; toggling calls setActionChecked then forces a re-render to reflect it.
  const [, forceRerender] = useState(0);

  const mandatoryCount = analysis.requiredActions.filter((a) => a.isMandatory).length;
  const nearestDeadlineDays = analysis.deadlines
    .filter((d): d is { date: string; description: typeof d.description } => !!d.date)
    .reduce<number | null>((min, d) => {
      const n = daysUntil(d.date);
      return min === null || n < min ? n : min;
    }, null);

  // Disclosure rules (see the implementation plan): Summary/Explanation never collapse. The
  // three list sections default collapsed, except when their own content is high-stakes enough
  // that hiding it by default would be the wrong call — a mandatory action, or a deadline within
  // a week (including one already overdue).
  const [keyPointsExpanded, toggleKeyPoints] = useSectionExpanded(documentId, "KeyPoints", false);
  const [actionsExpanded, toggleActions] = useSectionExpanded(documentId, "Actions", mandatoryCount > 0);
  const [deadlinesExpanded, toggleDeadlines] = useSectionExpanded(
    documentId, "Deadlines", nearestDeadlineDays !== null && nearestDeadlineDays <= 7
  );

  // Stop and release any audio when the component unmounts.
  useEffect(() => {
    return () => {
      audioRef.current?.pause();
      if (audioRef.current?.src) URL.revokeObjectURL(audioRef.current.src);
    };
  }, []);

  // Marks that the visitor has seen a completed analysis at least once — gates InstallPrompt,
  // which only offers "Add to Home Screen" once the product's value has actually landed.
  useEffect(() => {
    markAnalysisSeen();
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
      let audio: HTMLAudioElement;
      if (audioSrcFor) {
        const src = audioSrcFor("Full", language);
        if (!src) throw new Error(t("doc.speechError"));
        audio = new Audio(src);
      } else {
        const blob = trial || !documentId
          ? await api.trialSpeech(analysis, LANGUAGE_ENUM[language])
          : await api.speech(documentId, LANGUAGE_ENUM[language]);
        audio = new Audio(URL.createObjectURL(blob));
      }
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
      {/* Sticky so the audio control stays reachable while scrolling the result. md:top-32
          (not top-20) on desktop, where BottomNav now adds a second fixed row under Navbar —
          top-20 alone would put this player right underneath it. */}
      <div data-print-hide className="sticky top-20 z-30 flex flex-col gap-1 md:top-32">
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
              <span className="absolute text-[0.5rem] font-bold">5</span>
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

      {/* Answers "what is this / must I act / by when" before any of the cards below — see the
          implementation plan. Pure re-ranking of the same AnalysisResult, not a new data source. */}
      <AnalysisVerdict analysis={analysis} />

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div className="flex items-center gap-1">
            <CardTitle>{t("doc.summary")}</CardTitle>
            <SectionAudioButton analysis={analysis} documentId={documentId} trial={trial} section="Summary" label={t("doc.playSummary")} audioSrcFor={audioSrcFor} />
          </div>
          <span
            className="rounded-full border px-3 py-1 text-sm font-medium"
            style={{
              color: `var(--urgency-${URGENCY_VAR_BY_INDEX[urgencyIndex]}-fg)`,
              background: `var(--urgency-${URGENCY_VAR_BY_INDEX[urgencyIndex]}-bg)`,
              borderColor: `var(--urgency-${URGENCY_VAR_BY_INDEX[urgencyIndex]}-border)`,
            }}
          >
            {t("doc.urgency")}: {t(URGENCY_KEYS[urgencyIndex] ?? "urg.low")}
          </span>
        </CardHeader>
        <CardContent>
          <p className="text-gray-700 dark:text-gray-300" dir={dir}>{loc(analysis.summary, language)}</p>
          <p className="mt-2 text-sm text-gray-500 dark:text-gray-400" dir={dir}>{t("doc.type")}: {loc(analysis.documentType, language)}</p>
        </CardContent>
      </Card>

      {/* Directly under the summary, matching the spoken walkthrough's order (summary, then
          explanation) so a listener following along sees the same passage being read aloud. */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-lg">{t("doc.explanation")}</CardTitle>
          <SectionAudioButton analysis={analysis} documentId={documentId} trial={trial} section="Explanation" label={t("doc.playExplanation")} audioSrcFor={audioSrcFor} />
        </CardHeader>
        <CardContent className="space-y-3" dir={dir}>
          {/* Chunked into paragraphs instead of one whitespace-pre-wrap block, so a long
              explanation reads as separated thoughts rather than a wall of text. */}
          {loc(analysis.explanation, language).split(/\n{2,}/).map((para, i) => (
            <p key={i} className="whitespace-pre-wrap text-gray-700 dark:text-gray-300">{para}</p>
          ))}
        </CardContent>
      </Card>

      <Card>
        <SectionToggleHeader
          icon={ListChecks}
          title={t("doc.keyPoints")}
          count={analysis.keyPoints.length}
          expanded={keyPointsExpanded}
          onToggle={toggleKeyPoints}
          audioButton={<SectionAudioButton analysis={analysis} documentId={documentId} trial={trial} section="KeyPoints" label={t("doc.playKeyPoints")} audioSrcFor={audioSrcFor} />}
        />
        {keyPointsExpanded && (
          <CardContent>
            <ul className="list-inside list-disc space-y-1 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.keyPoints.map((p, i) => <li key={i}>{loc(p, language)}</li>)}
            </ul>
          </CardContent>
        )}
      </Card>

      <Card>
        <SectionToggleHeader
          icon={CheckSquare}
          title={t("doc.actions")}
          count={analysis.requiredActions.length}
          expanded={actionsExpanded}
          onToggle={toggleActions}
          audioButton={<SectionAudioButton analysis={analysis} documentId={documentId} trial={trial} section="Actions" label={t("doc.playActions")} audioSrcFor={audioSrcFor} />}
        />
        {actionsExpanded && (
          <CardContent>
            <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.requiredActions.map((a, i) => {
                const checked = documentId ? isActionChecked(documentId, i) : false;
                return (
                  <li key={i}>
                    <div className="flex items-start gap-2">
                      {/* The visible box stays 16px (unaffected layout footprint), but its hit
                          area is a full 44px square (WCAG 2.5.5) via an absolutely-positioned
                          invisible <label> — position:absolute keeps it out of flow, so it
                          can't shift the AlertTriangle/text siblings, even though it visually
                          overlaps a few px into them. */}
                      <span className="relative mt-0.5 inline-block h-4 w-4 shrink-0">
                        <input
                          id={`action-${documentId ?? "trial"}-${i}`}
                          type="checkbox"
                          checked={checked}
                          disabled={!documentId}
                          onChange={(e) => {
                            if (!documentId) return;
                            setActionChecked(documentId, i, e.target.checked);
                            forceRerender((n) => n + 1);
                          }}
                          aria-label={loc(a.description, language)}
                          className="h-4 w-4 accent-brand"
                        />
                        <label
                          htmlFor={`action-${documentId ?? "trial"}-${i}`}
                          aria-hidden="true"
                          className="absolute -inset-3.5 cursor-pointer"
                        />
                      </span>
                      {a.isMandatory && (
                        <>
                          <AlertTriangle aria-hidden="true" className="mt-1 h-4 w-4 shrink-0 text-orange-500" />
                          <span
                            className="shrink-0 rounded-full px-2 py-0.5 text-xs font-medium"
                            style={{ color: "var(--mandatory-fg)", background: "var(--mandatory-bg)" }}
                          >
                            {t("doc.required")}
                          </span>
                        </>
                      )}
                      <span className={checked ? "line-through opacity-60" : undefined}>{loc(a.description, language)}</span>
                    </div>
                    {/* Deferred: real analyses don't set hebrewPhrase yet — see phraseAudioSrcFor doc. */}
                    {a.hebrewPhrase && (
                      <div className="ps-6">
                        <PhraseCard
                          phrase={a.hebrewPhrase}
                          contactPhone={a.contactPhone}
                          audioSrc={phraseAudioSrcFor ? phraseAudioSrcFor(i) : undefined}
                        />
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          </CardContent>
        )}
      </Card>

      <Card>
        <SectionToggleHeader
          icon={Calendar}
          title={t("doc.deadlines")}
          count={analysis.deadlines.length}
          expanded={deadlinesExpanded}
          onToggle={toggleDeadlines}
          audioButton={<SectionAudioButton analysis={analysis} documentId={documentId} trial={trial} section="Deadlines" label={t("doc.playDeadlines")} audioSrcFor={audioSrcFor} />}
        />
        {deadlinesExpanded && (
          <CardContent>
            {analysis.deadlines.length === 0 ? (
              <p className="text-gray-500 dark:text-gray-400">—</p>
            ) : (
              <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
                {analysis.deadlines.map((d, i) => (
                  <li key={i} className="flex flex-wrap items-center gap-2">
                    <span>
                      {d.date ? <strong>{new Date(d.date).toLocaleDateString()}</strong> : null} {loc(d.description, language)}
                    </span>
                    {d.date && (
                      <a
                        href={URL.createObjectURL(buildDeadlineIcs({ date: d.date, description: loc(d.description, language), documentName: t("app.name") }))}
                        download={`deadline-${i}.ics`}
                        className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline"
                      >
                        <CalendarPlus className="h-4 w-4" />{t("doc.addToCalendar")}
                      </a>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        )}
      </Card>

      {/* Sponsored referrals — now below the analysis's own content rather than sandwiched
          between Explanation and Key points, where it read as part of Fana's own findings. The
          card's brand-tinted chrome plus the "Sponsored" eyebrow (see ReferralBlock) are the
          boundary; renders nothing when no providers match. */}
      <ReferralBlock analysis={analysis} documentId={documentId} />
    </div>
  );
}
