"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function LoginPage() {
  const { t } = useLanguage();
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email, password);
      router.push("/dashboard");
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.loginTitle")}</h1>
      <Card>
        <CardHeader><CardTitle>{t("auth.loginTitle")}</CardTitle></CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-4">
            <Input type="email" aria-label={t("auth.email")} autoComplete="email" placeholder={t("auth.email")} value={email} onChange={(e) => setEmail(e.target.value)} required />
            <Input type="password" aria-label={t("auth.password")} autoComplete="current-password" placeholder={t("auth.password")} value={password} onChange={(e) => setPassword(e.target.value)} required />
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <Button type="submit" className="w-full" disabled={busy}>{busy ? t("common.loading") : t("nav.login")}</Button>
          </form>
          <p className="mt-3 text-center text-sm">
            <Link href="/forgot-password" className="text-brand hover:underline">{t("auth.forgotPassword")}</Link>
          </p>
          <p className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
            <Link href="/register" className="text-brand hover:underline">{t("nav.register")}</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
