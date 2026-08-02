"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Check, Copy, Gift, Share2 } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import type { CreateSponsorshipResult, SponsorshipSummary } from "@/lib/types";
import { OutOfCreditsPrompt, isOutOfCreditsError } from "@/components/OutOfCreditsPrompt";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function GiftPage() {
  const { t, rtl } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const dir = rtl ? "rtl" : "ltr";

  const [balance, setBalance] = useState<number | null>(null);
  const [phone, setPhone] = useState("");
  const [credits, setCredits] = useState(3);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [outOfCredits, setOutOfCredits] = useState(false);
  const [result, setResult] = useState<CreateSponsorshipResult | null>(null);
  const [copied, setCopied] = useState(false);
  const [history, setHistory] = useState<SponsorshipSummary[] | null>(null);

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  useEffect(() => {
    if (!user) return;
    api.getWallet().then((w) => setBalance(w.balance.balance)).catch(() => {});
    api.listSponsorships().then(setHistory).catch(() => {});
  }, [user]);

  if (!user) return <p className="text-gray-500 dark:text-gray-400">{t("common.loading")}</p>;

  const redeemUrl = result ? `${window.location.origin}/redeem/${result.redeemToken}` : "";

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await api.createSponsorship(phone, credits);
      setResult(res);
      setBalance((b) => (b === null ? b : b - res.credits));
      setHistory((h) => [{ id: res.id, credits: res.credits, redeemed: false, createdAt: new Date().toISOString(), expiresAt: res.expiresAt }, ...(h ?? [])]);
    } catch (err) {
      if (isOutOfCreditsError(err)) setOutOfCredits(true);
      else setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const copyLink = async () => {
    await navigator.clipboard.writeText(redeemUrl);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 2000);
  };

  const shareLink = async () => {
    const text = t("gift.shareMessage").replace("{url}", redeemUrl);
    if (navigator.share) {
      try {
        await navigator.share({ title: t("app.name"), text, url: redeemUrl });
        return;
      } catch {
        return;
      }
    }
    window.open(`https://wa.me/?text=${encodeURIComponent(text)}`, "_blank", "noopener");
  };

  if (outOfCredits) return <OutOfCreditsPrompt variant="authenticated" />;

  return (
    <div className="mx-auto max-w-2xl pt-6" dir={dir}>
      <div className="mb-6 flex items-center gap-3">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-gradient text-white">
          <Gift className="h-6 w-6" />
        </span>
        <div>
          <h1 className="text-xl font-bold">{t("gift.title")}</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("gift.intro")}</p>
        </div>
      </div>

      {result ? (
        <Card className="border-brand bg-brand-light dark:bg-brand/15">
          <CardContent className="flex flex-col items-center gap-4 py-8 text-center">
            <p className="text-lg font-medium">{t("gift.linkReadyTitle")}</p>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              {t("gift.linkReadyBody").replace("{n}", String(result.credits))}
            </p>
            <div className="w-full break-all rounded-xl border border-gray-200 bg-white px-4 py-3 text-start text-sm dark:border-gray-700 dark:bg-gray-900">
              {redeemUrl}
            </div>
            <div className="flex flex-wrap justify-center gap-3">
              <Button variant="outline" onClick={copyLink}>
                {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                {copied ? t("gift.copied") : t("gift.copyLink")}
              </Button>
              <Button onClick={shareLink}>
                <Share2 className="h-4 w-4" />{t("gift.shareLink")}
              </Button>
            </div>
            <Button variant="ghost" onClick={() => { setResult(null); setPhone(""); }}>
              {t("gift.sendAnother")}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t("gift.formTitle")}</CardTitle>
            {balance !== null && (
              <p className="text-sm text-gray-500 dark:text-gray-400">
                {t("gift.yourBalance").replace("{n}", String(balance))}
              </p>
            )}
          </CardHeader>
          <CardContent>
            <form onSubmit={submit} className="space-y-4">
              <Input
                type="tel"
                aria-label={t("gift.phoneLabel")}
                placeholder={t("gift.phonePlaceholder")}
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                required
              />
              <div>
                <label className="mb-1 block text-sm text-gray-600 dark:text-gray-400">{t("gift.creditsLabel")}</label>
                <Input
                  type="number"
                  min={1}
                  max={balance ?? undefined}
                  aria-label={t("gift.creditsLabel")}
                  value={credits}
                  onChange={(e) => setCredits(Math.max(1, Number(e.target.value)))}
                  required
                />
              </div>
              {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
              <Button type="submit" className="w-full" disabled={busy || (balance !== null && balance < 1)}>
                {busy ? t("common.loading") : t("gift.send")}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {history && history.length > 0 && (
        <Card className="mt-6">
          <CardHeader><CardTitle className="text-lg">{t("gift.historyTitle")}</CardTitle></CardHeader>
          <CardContent>
            <ul className="divide-y divide-gray-100 dark:divide-gray-800">
              {history.map((s) => (
                <li key={s.id} className="flex items-center justify-between py-3">
                  <div>
                    <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
                      {t("gift.creditsSent").replace("{n}", String(s.credits))}
                    </p>
                    <p className="text-xs text-gray-500 dark:text-gray-400">{new Date(s.createdAt).toLocaleDateString()}</p>
                  </div>
                  <span className={`text-sm font-medium ${s.redeemed ? "text-green-600" : "text-gray-400"}`}>
                    {s.redeemed ? t("gift.redeemed") : t("gift.pending")}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
