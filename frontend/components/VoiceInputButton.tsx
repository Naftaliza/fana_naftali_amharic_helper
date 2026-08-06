"use client";

import { useState } from "react";
import { Loader2, Mic, Square } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { useVoiceRecorder, type VoiceRecorderError } from "@/lib/useVoiceRecorder";
import { LANGUAGE_ENUM, type Language } from "@/lib/types";

// Stable error-code -> i18n-key mapping, same pattern as UploadExperience.tsx's
// ERROR_MESSAGE_KEYS — a user who can't read English shouldn't ever see a raw error string.
const ERROR_KEYS: Record<VoiceRecorderError | "TRANSCRIBE_FAILED", string> = {
  MIC_DENIED: "voice.micDenied",
  MIC_UNAVAILABLE: "voice.micUnavailable",
  MIC_UNSUPPORTED: "voice.micUnsupported",
  RECORDING_FAILED: "voice.recordingFailed",
  TRANSCRIBE_FAILED: "voice.transcribeFailed",
};

function fmt(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, "0")}`;
}

function extensionFor(mimeType: string): string {
  if (mimeType.includes("mp4")) return "m4a";
  if (mimeType.includes("webm")) return "webm";
  return "audio";
}

/**
 * Mic button that lets a user who can't type Amharic (or can't type at all) ask a question by
 * voice. Tap to start, tap to stop (never hold-to-talk — see useVoiceRecorder). The transcript
 * FILLS the question box via onTranscript; it is never auto-sent, since Amharic ASR can be
 * imperfect and auto-sending would spend a real chat credit on a question the user didn't ask.
 */
export function VoiceInputButton({
  language,
  trial = false,
  onTranscript,
  disabled,
}: {
  language: Language;
  trial?: boolean;
  onTranscript: (text: string) => void;
  disabled?: boolean;
}) {
  const { t } = useLanguage();
  const [transcribing, setTranscribing] = useState(false);
  const [errorKey, setErrorKey] = useState<string | null>(null);

  const handleResult = async (blob: Blob, mimeType: string) => {
    setErrorKey(null);
    setTranscribing(true);
    try {
      const fileName = `question.${extensionFor(mimeType)}`;
      const result = trial
        ? await api.trialTranscribe(blob, LANGUAGE_ENUM[language], fileName)
        : await api.transcribe(blob, LANGUAGE_ENUM[language], fileName);
      if (result.text.trim()) onTranscript(result.text.trim());
      else setErrorKey(ERROR_KEYS.TRANSCRIBE_FAILED);
    } catch {
      setErrorKey(ERROR_KEYS.TRANSCRIBE_FAILED);
    } finally {
      setTranscribing(false);
    }
  };

  const { recording, elapsedSeconds, error, start, stop } = useVoiceRecorder(handleResult);

  const toggle = () => {
    setErrorKey(null);
    if (recording) stop();
    else start();
  };

  const shownErrorKey = errorKey ?? (error ? ERROR_KEYS[error] : null);

  return (
    <div className="relative">
      <button
        type="button"
        onClick={toggle}
        disabled={disabled || transcribing}
        aria-label={recording ? t("voice.recording") : t("voice.start")}
        title={recording ? t("voice.recording") : t("voice.start")}
        className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl border transition-colors ${
          recording
            ? "border-red-300 bg-red-50 text-red-600 dark:border-red-800 dark:bg-red-950 dark:text-red-400"
            : "border-gray-200 bg-white text-gray-600 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800"
        } disabled:opacity-50`}
      >
        {transcribing ? (
          <Loader2 className="h-5 w-5 animate-spin" />
        ) : recording ? (
          <Square className="h-4 w-4 fill-current" />
        ) : (
          <Mic className="h-5 w-5" />
        )}
      </button>

      {recording && (
        <span
          role="status"
          className="absolute -top-6 start-1/2 -translate-x-1/2 whitespace-nowrap text-xs font-medium text-red-600 dark:text-red-400"
        >
          {fmt(elapsedSeconds)}
        </span>
      )}

      {shownErrorKey && (
        <p role="alert" className="absolute top-full mt-1 w-48 text-xs text-red-600 dark:text-red-400">
          {t(shownErrorKey)}
        </p>
      )}
    </div>
  );
}
