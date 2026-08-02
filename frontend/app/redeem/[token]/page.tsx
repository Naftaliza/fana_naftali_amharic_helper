"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Gift } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { setPendingRedeemToken } from "@/lib/pendingRedeemToken";
import { Button } from "@/components/ui/button";

/**
 * A gift link — /gift generates one of these. Two paths: already signed in → redeem immediately
 * on load; not signed in yet (the common case: a beneficiary's very first Fana link) → stash the
 * token and send them to register/login, where RedeemPendingPrompt finishes the job once they're
 * authenticated, wherever they land afterward.
 */
export default function RedeemPage() {
  const { t, rtl } = useLanguage();
  const { user, loading: authLoading } = useAuth();
  const params = useParams();
  const token = params.token as string;
  const dir = rtl ? "rtl" : "ltr";

  const [state, setState] = useState<"checking" | "needs-auth" | "redeeming" | "done" | "error">("checking");
  const [creditsGranted, setCreditsGranted] = useState(0);

  useEffect(() => {
    if (authLoading) return;
    if (!user) {
      setPendingRedeemToken(token);
      setState("needs-auth");
      return;
    }
    setState("redeeming");
    api.redeemSponsorship(token)
      .then((res) => { setCreditsGranted(res.creditsGranted); setState("done"); })
      .catch(() => setState("error"));
  }, [authLoading, user, token]);

  if (authLoading || state === "checking") {
    return <p className="text-gray-500 dark:text-gray-400">{t("common.loading")}</p>;
  }

  if (state === "needs-auth") {
    return (
      <div className="mx-auto flex max-w-md flex-col items-center gap-6 pt-16 text-center" dir={dir}>
        <Gift className="h-14 w-14 text-brand" />
        <h1 className="text-2xl font-bold">{t("gift.redeemNeedsAuthTitle")}</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">{t("gift.redeemNeedsAuthBody")}</p>
        <div className="flex w-full flex-col gap-3">
          <Link href="/register"><Button size="lg" className="w-full">{t("nav.register")}</Button></Link>
          <Link href="/login"><Button variant="outline" size="lg" className="w-full">{t("nav.login")}</Button></Link>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-6 pt-16 text-center" dir={dir}>
      <Gift className="h-14 w-14 text-brand" />
      {state === "redeeming" && <p className="text-lg text-gray-600 dark:text-gray-400">{t("gift.redeeming")}</p>}
      {state === "done" && (
        <>
          <h1 className="text-2xl font-bold text-brand">{t("gift.redeemedBanner").replace("{n}", String(creditsGranted))}</h1>
          <Link href="/wallet"><Button size="lg">{t("nav.wallet")}</Button></Link>
        </>
      )}
      {state === "error" && <p className="text-lg text-red-600">{t("gift.redeemError")}</p>}
    </div>
  );
}
