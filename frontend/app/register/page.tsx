"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye, EyeOff } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { getPersistedOrgSlug, useOrganization } from "@/lib/organization-context";
import { LANGUAGE_ENUM, loc } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const ERROR_MESSAGE_KEYS: Record<string, string> = {
  NETWORK_ERROR: "auth.networkError",
  RATE_LIMITED: "auth.tooManyAttempts",
};

export default function RegisterPage() {
  const { t, language } = useLanguage();
  const { register } = useAuth();
  const { organization } = useOrganization();
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (password !== confirmPassword) {
      setError(t("auth.passwordsDontMatch"));
      return;
    }
    setBusy(true);
    try {
      // Fall back to the persisted slug directly: a fast submit can beat the branding
      // fetch that populates `organization`, and the slug is known synchronously either way.
      const { email: confirmedEmail } = await register(
        email, password, name, LANGUAGE_ENUM[language], organization?.slug ?? getPersistedOrgSlug());
      router.push(`/check-email?email=${encodeURIComponent(confirmedEmail)}`);
    } catch (err) {
      const msg = (err as Error).message;
      setError(ERROR_MESSAGE_KEYS[msg] ? t(ERROR_MESSAGE_KEYS[msg]) : msg);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.registerTitle")}</h1>
      <Card>
        <CardHeader className={organization ? "text-center" : undefined}>
          {organization?.logoUrl && (
            <img
              src={organization.logoUrl}
              alt={`${organization.name} logo`}
              className="mb-2 h-12 w-auto max-w-[200px] object-contain"
              onError={(e) => { e.currentTarget.style.display = "none"; }}
            />
          )}
          <CardTitle>{organization ? `${t("auth.registerTitle")} — ${organization.name}` : t("auth.registerTitle")}</CardTitle>
          {organization && (
            <p
              className="mt-1 bg-clip-text text-sm font-medium text-transparent"
              style={{ backgroundImage: "linear-gradient(135deg, var(--org-primary), var(--org-accent))" }}
            >
              {loc(organization.welcomeText, language)}
            </p>
          )}
        </CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-4">
            <Input
              aria-label={t("auth.name")}
              autoComplete="name"
              autoFocus
              placeholder={t("auth.name")}
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
            <Input type="email" aria-label={t("auth.email")} autoComplete="email" placeholder={t("auth.email")} value={email} onChange={(e) => setEmail(e.target.value)} required />
            <div className="relative">
              <Input
                type={showPassword ? "text" : "password"}
                aria-label={t("auth.password")}
                autoComplete="new-password"
                placeholder={t("auth.password")}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="pe-11"
                minLength={8}
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
            <Input
              type={showPassword ? "text" : "password"}
              aria-label={t("auth.confirmPassword")}
              autoComplete="new-password"
              placeholder={t("auth.confirmPassword")}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              minLength={8}
              required
            />
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("auth.passwordHint")}</p>
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <p className="text-center text-xs text-gray-500 dark:text-gray-400">{t("auth.registerDisclaimer")}</p>
            <Button
              type="submit"
              className="w-full"
              disabled={busy}
              style={organization ? { background: "linear-gradient(135deg, var(--org-primary), var(--org-accent))" } : undefined}
            >
              {busy ? t("common.loading") : t("nav.register")}
            </Button>
          </form>
          <p className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
            <Link href="/login" className="text-brand hover:underline">{t("nav.login")}</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
