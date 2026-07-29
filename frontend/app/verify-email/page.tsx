"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function VerifyEmailPage() {
  const { t } = useLanguage();
  const { verifyEmail } = useAuth();
  const router = useRouter();

  // Read once on mount from the query string (set by the emailed verification link). Avoids
  // useSearchParams so this client page needs no Suspense boundary — same pattern as
  // reset-password/page.tsx.
  const [email, setEmail] = useState<string | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [paramsLoaded, setParamsLoaded] = useState(false);
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    setEmail(params.get("email"));
    setToken(params.get("token"));
    setParamsLoaded(true);
  }, []);

  const [status, setStatus] = useState<"verifying" | "success" | "error">("verifying");
  const [resendSent, setResendSent] = useState(false);
  const [busy, setBusy] = useState(false);
  const calledRef = useRef(false);

  useEffect(() => {
    if (!paramsLoaded || calledRef.current) return;
    calledRef.current = true;
    if (!email || !token) {
      setStatus("error");
      return;
    }
    verifyEmail(email, token)
      .then(() => {
        setStatus("success");
        setTimeout(() => router.push("/dashboard"), 1500);
      })
      .catch(() => setStatus("error"));
  }, [paramsLoaded, email, token, verifyEmail, router]);

  const resend = async () => {
    if (!email) return;
    setBusy(true);
    try {
      await api.resendVerification(email);
      setResendSent(true);
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
          {status === "verifying" && (
            <p className="text-sm text-gray-500">{t("auth.verifyEmailVerifying")}</p>
          )}
          {status === "success" && (
            <p className="text-sm text-green-600">{t("auth.verifyEmailSuccess")}</p>
          )}
          {status === "error" && (
            <>
              <p role="alert" className="text-sm text-red-600">{t("auth.verifyLinkInvalid")}</p>
              {resendSent ? (
                <p className="text-sm text-green-600">{t("auth.resendVerificationSent")}</p>
              ) : (
                <Button variant="outline" className="w-full" disabled={busy || !email} onClick={resend}>
                  {busy ? t("common.loading") : t("auth.resendVerification")}
                </Button>
              )}
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                {t("auth.wrongEmail")}{" "}
                <Link href="/register" className="text-brand hover:underline">{t("auth.editRegistration")}</Link>
              </p>
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
