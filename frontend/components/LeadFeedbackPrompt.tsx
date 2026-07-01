"use client";

import { useEffect, useState } from "react";
import { ThumbsDown, ThumbsUp, X } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { clearPendingFeedback, getPendingFeedback, type PendingFeedback } from "@/lib/leadFeedback";
import { Card, CardContent } from "@/components/ui/card";

// Give the user real time to actually message/call before asking if it helped.
const MIN_WAIT_MS = 60_000;

/**
 * Post-contact "did they help?" prompt (Feature 6). Shown once, some time after the user
 * taps WhatsApp/call on a ReferralBlock provider (tracked in lib/leadFeedback.ts), so admins
 * get a real quality signal instead of billing on the raw contact tap alone.
 */
export function LeadFeedbackPrompt() {
  const { t, rtl } = useLanguage();
  const [pending, setPending] = useState<PendingFeedback | null>(null);
  const [done, setDone] = useState(false);

  useEffect(() => {
    const check = () => {
      const p = getPendingFeedback();
      if (p && Date.now() - p.contactedAt >= MIN_WAIT_MS) setPending(p);
    };
    check();
    // Re-check when the user comes back to the tab (e.g. after WhatsApp) or after a delay.
    document.addEventListener("visibilitychange", check);
    const timer = window.setInterval(check, 15_000);
    return () => {
      document.removeEventListener("visibilitychange", check);
      window.clearInterval(timer);
    };
  }, []);

  if (!pending) return null;

  const dismiss = () => {
    clearPendingFeedback();
    setPending(null);
  };

  const rate = async (helpful: boolean) => {
    setDone(true);
    try {
      await api.submitLeadFeedback(pending.ref, helpful);
    } catch {
      // best-effort — the prompt closes either way
    }
    window.setTimeout(() => { clearPendingFeedback(); setPending(null); setDone(false); }, 1500);
  };

  return (
    <div
      dir={rtl ? "rtl" : "ltr"}
      className="pb-safe fixed inset-x-4 bottom-4 z-50 mx-auto max-w-sm sm:inset-x-auto sm:end-4"
    >
      <Card className="shadow-soft">
        <CardContent className="relative py-4">
          <button
            onClick={dismiss}
            aria-label={t("onboarding.skip")}
            className="absolute end-3 top-3 rounded-lg p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <X className="h-4 w-4" />
          </button>
          {done ? (
            <p className="pe-6 text-sm font-medium text-brand">{t("feedback.thanks")}</p>
          ) : (
            <>
              <p className="pe-6 text-sm font-medium">
                {t("feedback.title").replace("{name}", pending.providerName)}
              </p>
              <div className="mt-3 flex gap-2">
                <button
                  onClick={() => rate(true)}
                  className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-brand px-3 py-2 text-sm font-medium text-white hover:bg-brand-dark"
                >
                  <ThumbsUp className="h-4 w-4" />{t("feedback.yes")}
                </button>
                <button
                  onClick={() => rate(false)}
                  className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-gray-300 px-3 py-2 text-sm font-medium hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800"
                >
                  <ThumbsDown className="h-4 w-4" />{t("feedback.no")}
                </button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
