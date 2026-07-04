"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { getPersistedOrgSlug, useOrganization } from "@/lib/organization-context";
import { LANGUAGE_ENUM } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function RegisterPage() {
  const { t, language } = useLanguage();
  const { register } = useAuth();
  const { organization } = useOrganization();
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      // Fall back to the persisted slug directly: a fast submit can beat the branding
      // fetch that populates `organization`, and the slug is known synchronously either way.
      await register(email, password, name, LANGUAGE_ENUM[language], organization?.slug ?? getPersistedOrgSlug());
      router.push("/dashboard");
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-md pt-10">
      <h1 className="sr-only">{t("auth.registerTitle")}</h1>
      <Card>
        <CardHeader>
          <CardTitle>{organization ? `${t("auth.registerTitle")} — ${organization.name}` : t("auth.registerTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-4">
            <Input aria-label={t("auth.name")} autoComplete="name" placeholder={t("auth.name")} value={name} onChange={(e) => setName(e.target.value)} required />
            <Input type="email" aria-label={t("auth.email")} autoComplete="email" placeholder={t("auth.email")} value={email} onChange={(e) => setEmail(e.target.value)} required />
            <Input type="password" aria-label={t("auth.password")} autoComplete="new-password" placeholder={t("auth.password")} value={password} onChange={(e) => setPassword(e.target.value)} minLength={8} required />
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <Button type="submit" className="w-full" disabled={busy}>{busy ? t("common.loading") : t("nav.register")}</Button>
          </form>
          <p className="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
            <Link href="/login" className="text-brand hover:underline">{t("nav.login")}</Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
