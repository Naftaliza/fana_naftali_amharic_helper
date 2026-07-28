"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { FileText, Minus, Plus, X } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";

interface PageAsset {
  url: string;
  isPdf: boolean;
}

const MIN_ZOOM = 1;
const MAX_ZOOM = 3;
const ZOOM_STEP = 0.5;

/**
 * Thumbnail strip of the pages the user uploaded/photographed, with a tap-to-zoom lightbox
 * for images. Shown alongside the AI analysis so the user can verify the app read the right
 * document, check a number or address up close, and see what they photographed even when
 * OCR failed. Pages are fetched as blobs (an <img> can't carry the bearer token the API
 * requires) and their object URLs are revoked on unmount, same discipline as CameraCapture.
 */
export function DocumentPages({ documentId, totalPages }: { documentId: string; totalPages: number }) {
  const { t } = useLanguage();
  const [pages, setPages] = useState<(PageAsset | null)[]>(() => Array(totalPages).fill(null));
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const [zoom, setZoom] = useState(1);
  // Render the lightbox via a portal to document.body so its `fixed inset-0` resolves against
  // the viewport, not this component's `animate-fade-in-up` ancestor — a CSS `transform` (even
  // at rest, once the animation's final keyframe applies it) makes that ancestor a containing
  // block for fixed descendants, clipping the lightbox to the card instead of the full screen.
  // Same fix CameraCapture.tsx uses for its own full-screen overlay, and the same reason.
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  useEffect(() => {
    let cancelled = false;
    const urls: string[] = [];

    (async () => {
      for (let i = 0; i < totalPages; i++) {
        try {
          const blob = await api.documentPage(documentId, i);
          if (cancelled) return;
          const url = URL.createObjectURL(blob);
          urls.push(url);
          const isPdf = blob.type === "application/pdf";
          setPages((prev) => {
            const next = [...prev];
            next[i] = { url, isPdf };
            return next;
          });
        } catch {
          // Leave this page's slot null — the thumbnail strip just skips it.
        }
      }
    })();

    return () => {
      cancelled = true;
      urls.forEach((u) => URL.revokeObjectURL(u));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [documentId, totalPages]);

  if (totalPages === 0) return null;

  const openImage = (i: number) => {
    setZoom(1);
    setOpenIndex(i);
  };

  const openPage = pages[openIndex ?? -1] ?? null;

  return (
    <div className="animate-fade-in-up">
      <p className="mb-2 text-sm font-medium text-gray-500 dark:text-gray-400">{t("doc.pages")}</p>
      <div className="flex gap-3 overflow-x-auto pb-1">
        {pages.map((p, i) => (
          <button
            key={i}
            onClick={() => (p ? (p.isPdf ? window.open(p.url, "_blank", "noopener") : openImage(i)) : undefined)}
            disabled={!p}
            aria-label={`${t("doc.pageLabel")} ${i + 1}${p?.isPdf ? ` — ${t("doc.openPdf")}` : ""}`}
            className="relative grid h-20 w-16 shrink-0 place-items-center overflow-hidden rounded-lg border border-gray-200 bg-gray-50 disabled:opacity-50 dark:border-gray-700 dark:bg-gray-800"
          >
            {p ? (
              p.isPdf ? (
                <FileText className="h-7 w-7 text-brand" />
              ) : (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={p.url} alt="" className="h-full w-full object-cover" />
              )
            ) : (
              <span className="h-5 w-5 animate-pulse rounded-full bg-gray-200 dark:bg-gray-700" />
            )}
            <span className="absolute start-1 top-1 rounded bg-black/60 px-1.5 text-xs text-white">{i + 1}</span>
          </button>
        ))}
      </div>

      {mounted && openIndex !== null && openPage && !openPage.isPdf && createPortal(
        <div
          role="dialog"
          aria-modal="true"
          aria-label={`${t("doc.pageLabel")} ${openIndex + 1}`}
          className="fixed inset-0 z-50 flex flex-col bg-black/95"
        >
          <div className="flex items-center justify-between p-4">
            <button
              onClick={() => setOpenIndex(null)}
              aria-label={t("doc.closeImage")}
              className="grid h-11 w-11 place-items-center rounded-full bg-white/15 text-white"
            >
              <X className="h-6 w-6" />
            </button>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setZoom((z) => Math.max(MIN_ZOOM, +(z - ZOOM_STEP).toFixed(2)))}
                disabled={zoom <= MIN_ZOOM}
                aria-label={t("doc.zoomOut")}
                className="grid h-11 w-11 place-items-center rounded-full bg-white/15 text-white disabled:opacity-40"
              >
                <Minus className="h-5 w-5" />
              </button>
              <button
                onClick={() => setZoom((z) => Math.min(MAX_ZOOM, +(z + ZOOM_STEP).toFixed(2)))}
                disabled={zoom >= MAX_ZOOM}
                aria-label={t("doc.zoomIn")}
                className="grid h-11 w-11 place-items-center rounded-full bg-white/15 text-white disabled:opacity-40"
              >
                <Plus className="h-5 w-5" />
              </button>
            </div>
          </div>
          <div className="flex-1 touch-pinch-zoom overflow-auto">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={openPage.url}
              alt={`${t("doc.pageLabel")} ${openIndex + 1}`}
              style={{ transform: `scale(${zoom})`, transformOrigin: "center top" }}
              className="mx-auto h-auto w-full max-w-3xl transition-transform"
            />
          </div>
        </div>,
        document.body
      )}
    </div>
  );
}
