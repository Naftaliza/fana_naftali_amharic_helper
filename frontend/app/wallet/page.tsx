"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Gift, Wallet as WalletIcon } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { FEATURE_WALLET } from "@/lib/featureFlags";
import { useFeatureGate } from "@/lib/useFeatureGate";
import type { WalletSummary } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

// One entry's label — mirrors the backend Operation strings ("analyze" | "tts" | "chat" |
// "free_tier" | "grant" | "sponsorship_sent" | "sponsorship_received") plus the fallback for
// anything not yet mapped. free_tier_monthly is kept for historical ledger rows written before
// the free tier became one-time rather than renewing (see WalletService.TryConsumeAsync).
const OPERATION_KEYS: Record<string, string> = {
  analyze: "wallet.opAnalyze",
  tts: "wallet.opTts",
  chat: "wallet.opChat",
  free_tier: "wallet.opFreeTier",
  free_tier_monthly: "wallet.opFreeTier",
  grant: "wallet.opGrant",
  sponsorship_sent: "wallet.opSponsorshipSent",
  sponsorship_received: "wallet.opSponsorshipReceived",
};

export default function WalletPage() {
  const { t, rtl } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const enabled = useFeatureGate(FEATURE_WALLET);
  const [summary, setSummary] = useState<WalletSummary | null>(null);
  const [error, setError] = useState(false);
  const dir = rtl ? "rtl" : "ltr";

  useEffect(() => {
    // Skip entirely when the feature is off — otherwise this races useFeatureGate's own
    // redirect for an anonymous visitor, and "must sign in first" (a real, always-reachable
    // page) wins over "this feature doesn't exist right now", which defeats the point.
    if (!enabled) return;
    if (!loading && !user) router.push("/login");
  }, [enabled, loading, user, router]);

  useEffect(() => {
    if (!user) return;
    api.getWallet().then(setSummary).catch(() => setError(true));
  }, [user]);

  if (!enabled) return null;
  if (!user) return <p className="text-gray-500 dark:text-gray-400">{t("common.loading")}</p>;

  return (
    <div className="mx-auto max-w-2xl pt-6" dir={dir}>
      <div className="mb-6 flex items-center gap-3">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-gradient text-white">
          <WalletIcon className="h-6 w-6" />
        </span>
        <h1 className="text-xl font-bold">{t("wallet.title")}</h1>
      </div>

      {error && <p role="alert" className="text-red-600">{t("wallet.loadError")}</p>}

      {!error && !summary && <p className="text-gray-500 dark:text-gray-400">{t("common.loading")}</p>}

      {summary && (
        <div className="grid gap-4">
          <Card>
            <CardContent className="flex items-center justify-between py-6">
              <div>
                <p className="text-sm text-gray-500 dark:text-gray-400">{t("wallet.balance")}</p>
                <p className="text-3xl font-bold text-brand">{summary.balance.balance}</p>
              </div>
              <p className="max-w-[14rem] text-end text-sm text-gray-500 dark:text-gray-400">
                {t("wallet.freeTierNote").replace("{n}", String(summary.balance.freeTierCredits))}
              </p>
            </CardContent>
          </Card>

          <Link href="/gift">
            <Button variant="outline" className="w-full">
              <Gift className="h-4 w-4" />{t("nav.gift")}
            </Button>
          </Link>

          <Card>
            <CardHeader><CardTitle className="text-lg">{t("wallet.historyTitle")}</CardTitle></CardHeader>
            <CardContent>
              {summary.history.length === 0 ? (
                <p className="text-sm text-gray-500 dark:text-gray-400">{t("wallet.historyEmpty")}</p>
              ) : (
                <ul className="divide-y divide-gray-100 dark:divide-gray-800">
                  {summary.history.map((entry, i) => (
                    <li key={i} className="flex items-center justify-between py-3">
                      <div>
                        <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
                          {t(OPERATION_KEYS[entry.operation] ?? "wallet.opOther")}
                        </p>
                        <p className="text-xs text-gray-500 dark:text-gray-400">
                          {new Date(entry.createdAt).toLocaleString()}
                        </p>
                      </div>
                      <span className={`font-semibold ${entry.delta > 0 ? "text-green-600" : "text-gray-500 dark:text-gray-400"}`}>
                        {entry.delta > 0 ? `+${entry.delta}` : entry.delta}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
