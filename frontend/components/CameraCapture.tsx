"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Camera, X, SwitchCamera, RotateCcw, Check, ImageUp } from "lucide-react";
import { useLanguage } from "@/lib/language-context";

type Facing = "environment" | "user";

/**
 * Full-screen in-app camera. Shows a live preview, captures a frame to a JPEG File,
 * lets the user review (retake / use), and supports switching front/back cameras.
 * Requires a secure context (https or localhost).
 */
export function CameraCapture({
  onCapture,
  onClose,
  onChooseFile,
}: {
  onCapture: (file: File) => void;
  onClose: () => void;
  onChooseFile: () => void;
}) {
  const { t } = useLanguage();
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [facing, setFacing] = useState<Facing>("environment");
  const [preview, setPreview] = useState<{ url: string; file: File } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [starting, setStarting] = useState(true);

  const stop = useCallback(() => {
    streamRef.current?.getTracks().forEach((tr) => tr.stop());
    streamRef.current = null;
  }, []);

  const startStream = useCallback(async (mode: Facing) => {
    setStarting(true);
    setError(null);
    stop();
    try {
      if (!navigator.mediaDevices?.getUserMedia) {
        setError(t("camera.denied"));
        return;
      }
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: mode },
        audio: false,
      });
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play().catch(() => {});
      }
    } catch {
      setError(t("camera.denied"));
    } finally {
      setStarting(false);
    }
  }, [stop, t]);

  // Start on mount and whenever the camera is flipped; stop on unmount.
  useEffect(() => {
    if (!preview) startStream(facing);
    return () => stop();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [facing]);

  useEffect(() => () => stop(), [stop]);

  const capture = () => {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    const canvas = document.createElement("canvas");
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext("2d")?.drawImage(video, 0, 0);
    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        const file = new File([blob], "photo.jpg", { type: "image/jpeg" });
        setPreview({ url: URL.createObjectURL(blob), file });
        stop(); // freeze on the captured shot; turn the camera off during review
      },
      "image/jpeg",
      0.92
    );
  };

  const retake = () => {
    if (preview) URL.revokeObjectURL(preview.url);
    setPreview(null);
    startStream(facing);
  };

  const use = () => {
    if (!preview) return;
    onCapture(preview.file);
    URL.revokeObjectURL(preview.url);
  };

  const close = () => { stop(); onClose(); };

  // Close on Escape, and restore focus to the element that opened the dialog.
  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") close(); };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      opener?.focus?.();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t("upload.takePhoto")}
      className="pt-safe pb-safe fixed inset-0 z-50 flex flex-col bg-black"
    >
      {/* Top bar */}
      <div className="flex items-center justify-between p-4">
        <button onClick={close} aria-label={t("camera.cancel")} className="grid h-11 w-11 place-items-center rounded-full bg-white/15 text-white">
          <X className="h-6 w-6" />
        </button>
        {!preview && !error && (
          <button onClick={() => setFacing((f) => (f === "environment" ? "user" : "environment"))}
            aria-label={t("camera.switch")} className="grid h-11 w-11 place-items-center rounded-full bg-white/15 text-white">
            <SwitchCamera className="h-6 w-6" />
          </button>
        )}
      </div>

      {/* Viewfinder / preview / error */}
      <div className="relative flex-1 overflow-hidden">
        {error ? (
          <div className="flex h-full flex-col items-center justify-center gap-5 px-8 text-center text-white">
            <Camera className="h-14 w-14 opacity-70" />
            <p className="text-lg">{error}</p>
            <button onClick={() => { close(); onChooseFile(); }}
              className="flex items-center gap-2 rounded-xl bg-white px-5 py-3 font-medium text-gray-900">
              <ImageUp className="h-5 w-5" />{t("upload.orChooseFile")}
            </button>
          </div>
        ) : preview ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={preview.url} alt={t("camera.preview")} className="h-full w-full object-contain" />
        ) : (
          <>
            <video ref={videoRef} autoPlay playsInline muted className="h-full w-full object-cover" />
            {starting && (
              <div className="absolute inset-0 grid place-items-center text-white/80">{t("camera.starting")}</div>
            )}
          </>
        )}
      </div>

      {/* Controls */}
      {!error && (
        <div className="flex items-center justify-center gap-8 p-6">
          {preview ? (
            <>
              <button onClick={retake} className="flex flex-col items-center gap-1 text-white">
                <span className="grid h-14 w-14 place-items-center rounded-full bg-white/15"><RotateCcw className="h-7 w-7" /></span>
                <span className="text-sm">{t("camera.retake")}</span>
              </button>
              <button onClick={use} className="flex flex-col items-center gap-1 text-white">
                <span className="grid h-16 w-16 place-items-center rounded-full bg-brand"><Check className="h-8 w-8" /></span>
                <span className="text-sm">{t("camera.use")}</span>
              </button>
            </>
          ) : (
            <button onClick={capture} disabled={starting} aria-label={t("camera.capture")}
              className="h-20 w-20 rounded-full border-4 border-white bg-white/30 ring-4 ring-white/40 transition active:scale-95 disabled:opacity-50" />
          )}
        </div>
      )}
    </div>
  );
}
