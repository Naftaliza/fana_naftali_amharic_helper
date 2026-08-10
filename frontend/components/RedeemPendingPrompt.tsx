"use client";

import { useEffect, useState } from "react";
import { Gift, X } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { clearPendingRedeemToken, getPendingRedeemToken } from "@/lib/pendingRedeemToken";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Completes a gift redemption stashed by /redeem/[token] when the visitor wasn't signed in yet
 * (see lib/pendingRedeemToken.ts) — the beneficiary is often opening their very first Fana link
 * and has to register before anything can be credited to them. Auto-redeems on mount rather than
 * waiting for a tap (unlike SaveTrialAnalysisPrompt): following the link is already the clearest
 * signal of intent this flow has, so asking again would just be one more step for an audience
 * this app tries hard not to add steps for. Global singleton, same placement as
 * SaveTrialAnalysisPrompt/LeadFeedbackPrompt.
 */
export function RedeemPendingPrompt() {
  const { user } = useAuth();
  const { t, rtl } = useLanguage();
  const [token, setToken] = useState<string | null>(null);
  const [state, setState] = useState<"idle" | "redeeming" | "done" | "error">("idle");
  const [creditsGranted, setCreditsGranted] = useState(0);

  useEffect(() => {
    if (!user) return;
    const pending = getPendingRedeemToken();
    if (!pending) return;
    setToken(pending);
    setState("redeeming");
    api.redeemSponsorship(pending)
      .then((res) => {
        clearPendingRedeemToken();
        setCreditsGranted(res.creditsGranted);
        setState("done");
      })
      .catch(() => {
        clearPendingRedeemToken();
        setState("error");
      });
  }, [user]);

  if (!token || state === "idle") return null;

  const dismiss = () => setToken(null);

  return (
    <div
      dir={rtl ? "rtl" : "ltr"}
      data-a11y-fixed
      className="pb-safe fixed inset-x-4 bottom-20 z-50 mx-auto max-w-sm sm:inset-x-auto sm:start-4 md:bottom-4"
    >
      <Card className="shadow-soft">
        <CardContent className="relative py-4">
          {state !== "redeeming" && (
            <button
              onClick={dismiss}
              aria-label={t("onboarding.skip")}
              className="absolute end-3 top-3 rounded-lg p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
            >
              <X className="h-4 w-4" />
            </button>
          )}
          <div className="flex items-start gap-3 pe-6">
            <Gift className="mt-0.5 h-5 w-5 shrink-0 text-brand" />
            {state === "redeeming" && <p className="text-sm font-medium">{t("gift.redeeming")}</p>}
            {state === "done" && (
              <p className="text-sm font-medium text-brand">
                {t("gift.redeemedBanner").replace("{n}", String(creditsGranted))}
              </p>
            )}
            {state === "error" && <p className="text-sm text-red-600">{t("gift.redeemError")}</p>}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
