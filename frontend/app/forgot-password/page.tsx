"use client";

import { useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function ForgotPasswordPage() {
  const { t } = useLanguage();
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      // The backend always reports success here regardless of whether the email exists,
      // so there's nothing to branch on client-side — this is deliberate (no account
      // enumeration), not an oversight.
      await api.forgotPassword(email);
    } finally {
      setBusy(false);
      setSent(true);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.forgotPasswordTitle")}</h1>
      <Card>
        <CardHeader><CardTitle>{t("auth.forgotPasswordTitle")}</CardTitle></CardHeader>
        <CardContent>
          {sent ? (
            <p className="text-sm text-gray-600 dark:text-gray-400">{t("auth.forgotPasswordSent")}</p>
          ) : (
            <form onSubmit={submit} className="space-y-4">
              <p className="text-sm text-gray-600 dark:text-gray-400">{t("auth.forgotPasswordHint")}</p>
              <Input
                type="email" aria-label={t("auth.email")} autoComplete="email" placeholder={t("auth.email")}
                value={email} onChange={(e) => setEmail(e.target.value)} required
              />
              <Button type="submit" className="w-full" disabled={busy}>
                {busy ? t("common.loading") : t("auth.sendResetLink")}
              </Button>
            </form>
          )}
          <p className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
            <Link href="/login" className="text-brand hover:underline">{t("auth.backToLogin")}</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
