"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export type VoiceRecorderError = "MIC_DENIED" | "MIC_UNAVAILABLE" | "MIC_UNSUPPORTED" | "RECORDING_FAILED";

// Hard 60s auto-stop, mirroring the backend's [RequestSizeLimit(8_000_000)] cap on the
// transcribe endpoint — a minute of compressed speech comfortably fits under 8MB.
const MAX_SECONDS = 60;

// In preference order: Opus-in-WebM is what Android Chrome produces and what Azure Fast
// Transcription documents support directly (no server-side transcoding needed); MP4/AAC covers
// iOS Safari; the trailing "" lets the browser pick its own default if neither is supported.
const MIME_CANDIDATES = ["audio/webm;codecs=opus", "audio/webm", "audio/mp4", ""];

function pickMimeType(): string {
  if (typeof MediaRecorder === "undefined" || !MediaRecorder.isTypeSupported) return "";
  for (const mime of MIME_CANDIDATES) {
    if (mime === "" || MediaRecorder.isTypeSupported(mime)) return mime;
  }
  return "";
}

/**
 * Tap-to-start / tap-to-stop voice recording for the chat "ask out loud" mic button.
 * Deliberately not hold-to-talk: unreliable on cheap Android touchscreens, impossible with
 * TalkBack, and fails the moment a finger slips. Always releases the microphone — on manual
 * stop, on the 60s auto-stop, on a MediaRecorder error, and on unmount.
 */
export function useVoiceRecorder(onResult: (blob: Blob, mimeType: string) => void) {
  const [recording, setRecording] = useState(false);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [error, setError] = useState<VoiceRecorderError | null>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const cleanup = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    recorderRef.current = null;
    setRecording(false);
    setElapsedSeconds(0);
  }, []);

  const stop = useCallback(() => {
    // onstop (registered in start()) does the actual cleanup + onResult call.
    recorderRef.current?.stop();
  }, []);

  const start = useCallback(async () => {
    setError(null);
    if (typeof window === "undefined" || !navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === "undefined") {
      setError("MIC_UNSUPPORTED");
      return;
    }

    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch (err) {
      setError((err as DOMException)?.name === "NotAllowedError" ? "MIC_DENIED" : "MIC_UNAVAILABLE");
      return;
    }
    streamRef.current = stream;

    const mimeType = pickMimeType();
    const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);
    chunksRef.current = [];

    recorder.ondataavailable = (e) => {
      if (e.data.size > 0) chunksRef.current.push(e.data);
    };
    recorder.onerror = () => {
      setError("RECORDING_FAILED");
      cleanup();
    };
    recorder.onstop = () => {
      const blob = new Blob(chunksRef.current, { type: recorder.mimeType || mimeType || "audio/webm" });
      cleanup();
      if (blob.size > 0) onResult(blob, blob.type);
    };

    recorderRef.current = recorder;
    recorder.start();
    setRecording(true);
    setElapsedSeconds(0);
    timerRef.current = setInterval(() => {
      setElapsedSeconds((s) => {
        const next = s + 1;
        if (next >= MAX_SECONDS) stop();
        return next;
      });
    }, 1000);
  }, [cleanup, onResult, stop]);

  // Release the mic even if the component unmounts mid-recording.
  useEffect(() => () => cleanup(), [cleanup]);

  return { recording, elapsedSeconds, error, start, stop };
}
