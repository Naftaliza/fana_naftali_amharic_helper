"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const ERROR_MESSAGE_KEYS: Record<string, string> = {
  NETWORK_ERROR: "auth.networkError",
  RATE_LIMITED: "auth.tooManyAttempts",
};

export default function CheckEmailPage() {
  const { t } = useLanguage();
  // Read once on mount from the query string set by the register page redirect. Avoids
  // useSearchParams so this client page needs no Suspense boundary — same pattern as
  // reset-password/page.tsx.
  const [email, setEmail] = useState<string | null>(null);
  useEffect(() => {
    setEmail(new URLSearchParams(window.location.search).get("email"));
  }, []);

  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  const resend = async () => {
    if (!email) return;
    setBusy(true);
    setError(null);
    try {
      await api.resendVerification(email);
      setSent(true);
    } catch (err) {
      const msg = (err as Error).message;
      setError(ERROR_MESSAGE_KEYS[msg] ? t(ERROR_MESSAGE_KEYS[msg]) : msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.checkEmailTitle")}</h1>
      <Card>
        <CardHeader><CardTitle>{t("auth.checkEmailTitle")}</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t("auth.checkEmailBodyPrefix")} <strong>{email}</strong>.
          </p>
          {sent ? (
            <p className="text-sm text-green-600">{t("auth.resendVerificationSent")}</p>
          ) : (
            <>
              {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
              <Button variant="outline" className="w-full" disabled={busy || !email} onClick={resend}>
                {busy ? t("common.loading") : t("auth.resendVerification")}
              </Button>
            </>
          )}
          <p className="text-center text-sm text-gray-600 dark:text-gray-400">
            <Link href="/login" className="text-brand hover:underline">{t("auth.backToLogin")}</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
