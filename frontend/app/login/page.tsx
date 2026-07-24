"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye, EyeOff } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const ERROR_MESSAGE_KEYS: Record<string, string> = {
  NETWORK_ERROR: "auth.networkError",
  RATE_LIMITED: "auth.tooManyAttempts",
};

export default function LoginPage() {
  const { t } = useLanguage();
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [unverifiedEmail, setUnverifiedEmail] = useState<string | null>(null);
  const [resendBusy, setResendBusy] = useState(false);
  const [resendSent, setResendSent] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setUnverifiedEmail(null);
    setResendSent(false);
    try {
      await login(email, password, remember);
      router.push("/dashboard");
    } catch (err) {
      const msg = (err as Error).message;
      if (msg === "EMAIL_NOT_VERIFIED") {
        setUnverifiedEmail(email);
        setError(t("auth.emailNotVerified"));
      } else {
        setError(ERROR_MESSAGE_KEYS[msg] ? t(ERROR_MESSAGE_KEYS[msg]) : msg);
      }
    } finally {
      setBusy(false);
    }
  };

  const resendVerification = async () => {
    if (!unverifiedEmail) return;
    setResendBusy(true);
    try {
      await api.resendVerification(unverifiedEmail);
      setResendSent(true);
    } finally {
      setResendBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.loginTitle")}</h1>
      <Card>
        <CardHeader><CardTitle>{t("auth.loginTitle")}</CardTitle></CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-4">
            <Input
              type="email"
              aria-label={t("auth.email")}
              autoComplete="email"
              autoFocus
              placeholder={t("auth.email")}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
            <div className="relative">
              <Input
                type={showPassword ? "text" : "password"}
                aria-label={t("auth.password")}
                autoComplete="current-password"
                placeholder={t("auth.password")}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="pe-11"
                required
              />
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="absolute inset-y-0 end-0 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                aria-label={showPassword ? t("auth.hidePassword") : t("auth.showPassword")}
                onClick={() => setShowPassword((v) => !v)}
              >
                {showPassword ? <EyeOff className="h-5 w-5" /> : <Eye className="h-5 w-5" />}
              </Button>
            </div>
            <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
              <input
                type="checkbox"
                checked={remember}
                onChange={(e) => setRemember(e.target.checked)}
                className="h-4 w-4 accent-brand"
              />
              {t("auth.rememberMe")}
            </label>
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            {unverifiedEmail && (
              resendSent ? (
                <p className="text-sm text-green-600">{t("auth.resendVerificationSent")}</p>
              ) : (
                <Button type="button" variant="outline" className="w-full" disabled={resendBusy} onClick={resendVerification}>
                  {resendBusy ? t("common.loading") : t("auth.resendVerification")}
                </Button>
              )
            )}
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
