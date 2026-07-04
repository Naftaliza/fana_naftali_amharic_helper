"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function ResetPasswordPage() {
  const { t } = useLanguage();
  const router = useRouter();
  // Read once on mount from the query string (set by the emailed reset link). Avoids
  // useSearchParams so this client page needs no Suspense boundary — same pattern as
  // documents/[id]/page.tsx's `skipped` param.
  const [email, setEmail] = useState<string | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [paramsLoaded, setParamsLoaded] = useState(false);
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    setEmail(params.get("email"));
    setToken(params.get("token"));
    setParamsLoaded(true);
  }, []);

  const [newPassword, setNewPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (newPassword !== confirm) {
      setError(t("auth.passwordsDontMatch"));
      return;
    }
    if (!email || !token) {
      setError(t("auth.resetLinkInvalid"));
      return;
    }
    setBusy(true);
    try {
      await api.resetPassword(email, token, newPassword);
      setDone(true);
      setTimeout(() => router.push("/login"), 2000);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.resetPasswordTitle")}</h1>
      <Card>
        <CardHeader><CardTitle>{t("auth.resetPasswordTitle")}</CardTitle></CardHeader>
        <CardContent>
          {done ? (
            <p className="text-sm text-gray-600 dark:text-gray-400">{t("auth.resetPasswordDone")}</p>
          ) : email && token ? (
            <form onSubmit={submit} className="space-y-4">
              <Input
                type="password" aria-label={t("auth.newPassword")} autoComplete="new-password" placeholder={t("auth.newPassword")}
                value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required minLength={8}
              />
              <Input
                type="password" aria-label={t("auth.confirmPassword")} autoComplete="new-password" placeholder={t("auth.confirmPassword")}
                value={confirm} onChange={(e) => setConfirm(e.target.value)} required minLength={8}
              />
              {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
              <Button type="submit" className="w-full" disabled={busy}>
                {busy ? t("common.loading") : t("auth.resetPassword")}
              </Button>
            </form>
          ) : !paramsLoaded ? (
            <p className="text-sm text-gray-500">{t("common.loading")}</p>
          ) : (
            <p role="alert" className="text-sm text-red-600">{t("auth.resetLinkInvalid")}</p>
          )}
          <p className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
            <Link href="/login" className="text-brand hover:underline">{t("auth.backToLogin")}</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
