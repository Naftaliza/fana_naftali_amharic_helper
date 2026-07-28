"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Camera, X, SwitchCamera, RotateCcw, Check, ImageUp, Plus, Trash2, ChevronUp, ChevronDown } from "lucide-react";
import { useLanguage } from "@/lib/language-context";

type Facing = "environment" | "user";
type Page = { url: string; file: File };

/**
 * Full-screen in-app camera supporting multi-page capture. Shows a live preview, captures a frame to
 * a JPEG File, lets the user review (retake / add), accumulates pages with a thumbnail strip
 * (remove / reorder), and returns all pages at once via onCapture. Requires a secure context.
 */
export function CameraCapture({
  onCapture,
  onClose,
  onChooseFile,
}: {
  onCapture: (files: File[]) => void;
  onClose: () => void;
  onChooseFile: () => void;
}) {
  const { t } = useLanguage();
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [facing, setFacing] = useState<Facing>("environment");
  const [preview, setPreview] = useState<{ url: string; file: File } | null>(null);
  const [pages, setPages] = useState<Page[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [starting, setStarting] = useState(true);
  // Render via a portal to document.body so the full-screen `fixed inset-0` resolves
  // against the viewport, not a transformed ancestor (the upload page's animated wrapper).
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  // Keep the latest pages/preview in a ref so the unmount cleanup revokes every object URL
  // without re-running (and tearing down the stream) on each capture.
  const cleanupRef = useRef<{ pages: Page[]; preview: { url: string } | null }>({ pages: [], preview: null });
  cleanupRef.current = { pages, preview };

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
      // Request a high-resolution, continuously-focused stream — browsers otherwise commonly
      // default to 640x480, which is a hard ceiling on OCR quality for dense document text.
      // These are hints (`ideal`), not hard requirements, so devices that can't meet them still connect.
      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: mode,
          width: { ideal: 1920 },
          height: { ideal: 1920 },
          // @ts-expect-error -- focusMode is part of the MediaTrackConstraints draft spec,
          // supported on Chromium/Android but not yet in TS's lib.dom types.
          focusMode: "continuous",
        },
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

  // On unmount, stop the stream and revoke every object URL (preview + all thumbnails).
  useEffect(() => () => {
    stop();
    if (cleanupRef.current.preview) URL.revokeObjectURL(cleanupRef.current.preview.url);
    cleanupRef.current.pages.forEach((p) => URL.revokeObjectURL(p.url));
  }, [stop]);

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
        const file = new File([blob], `photo-${pages.length + 1}.jpg`, { type: "image/jpeg" });
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

  // Accept the reviewed shot as a page and return to the live viewfinder for the next page.
  const addPage = () => {
    if (!preview) return;
    setPages((p) => [...p, preview]); // keep the URL alive for the thumbnail
    setPreview(null);
    startStream(facing);
  };

  // Finish immediately with the reviewed shot plus any pages already added — the one-tap
  // path for a single-page document, so users aren't forced through "Add page" then "Done".
  const useAndFinish = () => {
    if (!preview) return;
    onCapture([...pages.map((p) => p.file), preview.file]);
  };

  const removePage = (index: number) => {
    setPages((p) => {
      const target = p[index];
      if (target) URL.revokeObjectURL(target.url);
      return p.filter((_, i) => i !== index);
    });
  };

  const movePage = (index: number, dir: -1 | 1) => {
    setPages((p) => {
      const j = index + dir;
      if (j < 0 || j >= p.length) return p;
      const next = [...p];
      [next[index], next[j]] = [next[j], next[index]];
      return next;
    });
  };

  // Finish: hand all captured pages to the parent (revoking thumbnail URLs is left to unmount).
  const done = () => {
    if (pages.length === 0) return;
    onCapture(pages.map((p) => p.file));
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

  if (!mounted) return null;
  return createPortal(
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
        {pages.length > 0 && (
          <span className="rounded-full bg-white/15 px-3 py-1 text-sm font-medium text-white">
            {t("camera.pageCount").replace("{n}", String(pages.length))}
          </span>
        )}
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
            {!starting && (
              // Framing guide: corner brackets hinting at the page bounds, plus a hint that
              // fades after a few seconds. Capture still uses the full sensor frame regardless
              // of what's inside the guide — this is guidance, not a crop.
              <div className="pointer-events-none absolute inset-6 flex flex-col">
                <div className="relative flex-1">
                  <span className="absolute left-0 top-0 h-8 w-8 rounded-tl-lg border-l-4 border-t-4 border-white/80" />
                  <span className="absolute right-0 top-0 h-8 w-8 rounded-tr-lg border-r-4 border-t-4 border-white/80" />
                  <span className="absolute bottom-0 left-0 h-8 w-8 rounded-bl-lg border-b-4 border-l-4 border-white/80" />
                  <span className="absolute bottom-0 right-0 h-8 w-8 rounded-br-lg border-b-4 border-r-4 border-white/80" />
                </div>
                <p className="mt-3 self-center rounded-full bg-black/40 px-3 py-1 text-center text-sm text-white/90">
                  {t("camera.frameHint")}
                </p>
              </div>
            )}
            {starting && (
              <div className="absolute inset-0 grid place-items-center text-white/80">{t("camera.starting")}</div>
            )}
          </>
        )}
      </div>

      {/* Thumbnail strip of captured pages (hidden while reviewing a fresh shot) */}
      {!preview && !error && pages.length > 0 && (
        <div className="flex gap-3 overflow-x-auto px-4 py-3">
          {pages.map((p, i) => (
            <div key={p.url} className="relative shrink-0">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={p.url} alt={`${t("camera.pageLabel")} ${i + 1}`} className="h-20 w-16 rounded-lg object-cover" />
              <span className="absolute left-1 top-1 rounded bg-black/60 px-1.5 text-xs text-white">{i + 1}</span>
              <button onClick={() => removePage(i)} aria-label={t("camera.removePage")}
                className="absolute -right-2 -top-2 grid h-7 w-7 place-items-center rounded-full bg-red-600 text-white">
                <Trash2 className="h-4 w-4" />
              </button>
              <div className="mt-1 flex justify-center gap-1">
                <button onClick={() => movePage(i, -1)} disabled={i === 0} aria-label={t("camera.moveUp")}
                  className="grid h-7 w-7 place-items-center rounded bg-white/15 text-white disabled:opacity-30">
                  <ChevronUp className="h-4 w-4" />
                </button>
                <button onClick={() => movePage(i, 1)} disabled={i === pages.length - 1} aria-label={t("camera.moveDown")}
                  className="grid h-7 w-7 place-items-center rounded bg-white/15 text-white disabled:opacity-30">
                  <ChevronDown className="h-4 w-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Controls */}
      {!error && (
        <div className="flex items-center justify-center gap-8 p-6">
          {preview ? (
            <>
              <button onClick={retake} className="flex flex-col items-center gap-1 text-white">
                <span className="grid h-14 w-14 place-items-center rounded-full bg-white/15"><RotateCcw className="h-7 w-7" /></span>
                <span className="text-sm">{t("camera.retake")}</span>
              </button>
              <button onClick={useAndFinish} className="flex flex-col items-center gap-1 text-white">
                <span className="grid h-16 w-16 place-items-center rounded-full bg-brand"><Check className="h-8 w-8" /></span>
                <span className="text-sm">{t("camera.use")}</span>
              </button>
              <button onClick={addPage} className="flex flex-col items-center gap-1 text-white">
                <span className="grid h-14 w-14 place-items-center rounded-full bg-white/15"><Plus className="h-7 w-7" /></span>
                <span className="text-sm">{t("camera.addPage")}</span>
              </button>
            </>
          ) : (
            <>
              <button onClick={capture} disabled={starting} aria-label={t("camera.capture")}
                className="h-20 w-20 rounded-full border-4 border-white bg-white/30 ring-4 ring-white/40 transition active:scale-95 disabled:opacity-50" />
              {pages.length > 0 && (
                <button onClick={done} className="flex flex-col items-center gap-1 text-white">
                  <span className="grid h-16 w-16 place-items-center rounded-full bg-brand"><Check className="h-8 w-8" /></span>
                  <span className="text-sm">{t("camera.done")}</span>
                </button>
              )}
            </>
          )}
        </div>
      )}
    </div>,
    document.body
  );
}
