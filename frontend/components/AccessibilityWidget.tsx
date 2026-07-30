"use client";

import { useEffect, useRef, useState } from "react";
import {
  Eye, X, ZoomIn, ZoomOut, Contrast, Droplets, Type, Link2, RotateCcw, Check,
} from "lucide-react";
import { useLanguage } from "@/lib/language-context";

interface Settings {
  scale: number;          // root font-size multiplier
  contrast: boolean;
  grayscale: boolean;
  dyslexia: boolean;
  highlightLinks: boolean;
}

const DEFAULTS: Settings = { scale: 1, contrast: false, grayscale: false, dyslexia: false, highlightLinks: false };
const MIN = 0.8, MAX = 1.6, STEP = 0.1;

function applySettings(s: Settings) {
  const d = document.documentElement;
  d.classList.toggle("a11y-contrast", s.contrast);
  d.classList.toggle("a11y-grayscale", s.grayscale);
  d.classList.toggle("a11y-dyslexia", s.dyslexia);
  d.classList.toggle("a11y-highlight-links", s.highlightLinks);
  d.style.fontSize = s.scale === 1 ? "" : `${Math.round(s.scale * 100)}%`;
}

export function AccessibilityWidget() {
  const { t } = useLanguage();
  const [open, setOpen] = useState(false);
  const [settings, setSettings] = useState<Settings>(DEFAULTS);
  const ref = useRef<HTMLDivElement>(null);

  // Load saved settings (the inline script already applied them before paint).
  useEffect(() => {
    try {
      const saved = JSON.parse(localStorage.getItem("a11y") || "{}");
      setSettings({ ...DEFAULTS, ...saved });
    } catch { /* ignore */ }
  }, []);

  // Close on outside click / Escape.
  useEffect(() => {
    const onClick = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("mousedown", onClick); document.removeEventListener("keydown", onKey); };
  }, []);

  const update = (patch: Partial<Settings>) => {
    setSettings((prev) => {
      const next = { ...prev, ...patch };
      localStorage.setItem("a11y", JSON.stringify(next));
      applySettings(next);
      return next;
    });
  };

  const reset = () => {
    localStorage.removeItem("a11y");
    applySettings(DEFAULTS);
    setSettings(DEFAULTS);
  };

  const toggles: { key: keyof Settings; icon: typeof Contrast; label: string }[] = [
    { key: "contrast", icon: Contrast, label: t("a11y.contrast") },
    { key: "grayscale", icon: Droplets, label: t("a11y.grayscale") },
    { key: "dyslexia", icon: Type, label: t("a11y.dyslexia") },
    { key: "highlightLinks", icon: Link2, label: t("a11y.highlightLinks") },
  ];

  return (
    <div ref={ref} data-print-hide className="pb-safe fixed bottom-4 end-4 z-50 flex flex-col items-end gap-3">
      {open && (
        <div
          role="dialog"
          aria-label={t("a11y.title")}
          className="w-72 rounded-2xl border border-gray-100 bg-white p-4 shadow-soft dark:border-gray-800 dark:bg-gray-900"
        >
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg font-semibold">{t("a11y.title")}</h2>
            <button onClick={() => setOpen(false)} aria-label={t("camera.cancel")} className="rounded-lg p-1 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-800">
              <X className="h-5 w-5" />
            </button>
          </div>

          {/* Text size */}
          <p className="mb-1 text-sm font-medium text-gray-500 dark:text-gray-400">{t("a11y.textSize")}</p>
          <div className="mb-4 flex gap-2">
            <button
              onClick={() => update({ scale: Math.max(MIN, +(settings.scale - STEP).toFixed(2)) })}
              disabled={settings.scale <= MIN}
              className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-gray-200 py-2 text-sm hover:bg-gray-50 disabled:opacity-40 dark:border-gray-700 dark:hover:bg-gray-800"
            >
              <ZoomOut className="h-4 w-4" />{t("a11y.smaller")}
            </button>
            <button
              onClick={() => update({ scale: Math.min(MAX, +(settings.scale + STEP).toFixed(2)) })}
              disabled={settings.scale >= MAX}
              className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-gray-200 py-2 text-sm hover:bg-gray-50 disabled:opacity-40 dark:border-gray-700 dark:hover:bg-gray-800"
            >
              <ZoomIn className="h-4 w-4" />{t("a11y.larger")}
            </button>
          </div>

          {/* Toggles */}
          <div className="space-y-2">
            {toggles.map(({ key, icon: Icon, label }) => {
              const on = settings[key] as boolean;
              return (
                <button
                  key={key}
                  onClick={() => update({ [key]: !on } as Partial<Settings>)}
                  aria-pressed={on}
                  className="flex w-full items-center justify-between rounded-xl border border-gray-200 px-3 py-2.5 text-sm hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800"
                >
                  <span className="flex items-center gap-2"><Icon className="h-4 w-4 text-gray-600 dark:text-gray-300" />{label}</span>
                  <span className={`grid h-5 w-5 place-items-center rounded-full ${on ? "bg-accent text-white" : "border-2 border-gray-300 dark:border-gray-600"}`}>
                    {on && <Check className="h-3 w-3" />}
                  </span>
                </button>
              );
            })}
          </div>

          <button onClick={reset} className="mt-4 flex w-full items-center justify-center gap-2 rounded-xl border border-gray-200 py-2.5 text-sm text-gray-600 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800">
            <RotateCcw className="h-4 w-4" />{t("a11y.reset")}
          </button>
        </div>
      )}

      {/* Floating button */}
      <button
        onClick={() => setOpen((o) => !o)}
        aria-label={t("a11y.title")}
        aria-expanded={open}
        className="grid h-14 w-14 place-items-center rounded-full bg-accent text-white shadow-soft transition-transform hover:scale-105 active:scale-100"
      >
        <Eye className="h-7 w-7" />
      </button>
    </div>
  );
}
