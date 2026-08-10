"use client";

import { useState } from "react";
import { Download, X } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { useInstallPrompt } from "@/lib/useInstallPrompt";
import { hasSeenAnalysis } from "@/lib/analysisSeen";
import { hasDismissedInstallPrompt, markInstallPromptDismissed } from "@/lib/installPromptDismissed";

/**
 * Bottom banner offering to add Fana to the home screen. Shown only after a first analysis has
 * landed (hasSeenAnalysis) — never on cold load, since that's when the product's value is
 * actually obvious. Android/desktop Chrome gets a real one-tap install via beforeinstallprompt;
 * iOS Safari has no such API, so it gets illustrated Share -> Add to Home Screen steps instead.
 */
export function InstallPrompt() {
  const { t, rtl } = useLanguage();
  const { installed, ios, canPromptNatively, promptInstall } = useInstallPrompt();
  const [dismissed, setDismissed] = useState(() => hasDismissedInstallPrompt());
  const [showIosSteps, setShowIosSteps] = useState(false);

  if (installed || dismissed || !hasSeenAnalysis()) return null;
  if (!ios && !canPromptNatively) return null;

  const dismiss = () => {
    markInstallPromptDismissed();
    setDismissed(true);
  };

  return (
    <div
      dir={rtl ? "rtl" : "ltr"}
      data-print-hide
      data-a11y-fixed
      className="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-gray-100 bg-white/95 p-4 shadow-soft backdrop-blur dark:border-gray-800 dark:bg-gray-900/95"
    >
      <div className="mx-auto flex max-w-xl items-center gap-3">
        <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-gradient text-white" aria-hidden="true">
          <Download className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">{t("install.title")}</p>
          <p className="text-xs text-gray-500 dark:text-gray-400">{showIosSteps ? t("install.iosSteps") : t("install.body")}</p>
        </div>
        {!showIosSteps && (
          <button
            onClick={() => (ios ? setShowIosSteps(true) : promptInstall())}
            className="shrink-0 rounded-xl bg-brand px-3 py-2 text-sm font-semibold text-white hover:bg-brand-dark"
          >
            {t("install.add")}
          </button>
        )}
        <button
          onClick={dismiss}
          aria-label={t("install.dismiss")}
          className="shrink-0 rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
