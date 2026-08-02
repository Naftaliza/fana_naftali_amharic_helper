"use client";

import Link from "next/link";
import { Lock, Mail } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { SUPPORT_EMAIL } from "@/lib/support";
import { Button } from "@/components/ui/button";

/**
 * Shown wherever a metered call (analyze/speech/chat) comes back with the WalletErrors.OutOfCredits
 * code — see backend/.../WalletErrors.cs. Two variants because the right next step differs:
 * a signed-out visitor gets a fresh free tier just by registering (a UserId subject is separate
 * from the device-id subject they used anonymously); a signed-in user already has an account-tied
 * free tier and there's no self-serve top-up yet (no payment rail — see the plan), so the only
 * next step today is contacting support for a manual grant.
 */
export function OutOfCreditsPrompt({ variant }: { variant: "anonymous" | "authenticated" }) {
  const { t } = useLanguage();

  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-6 pt-16 text-center">
      <Lock className="h-14 w-14 text-brand" />
      {/* Reuses the same copy the old client-only "3 free tries" screen showed — still accurate
          now that the check happens server-side after a call instead of client-side before one. */}
      <h1 className="text-2xl font-bold">{t("trial.overTitle")}</h1>
      <p className="text-lg text-gray-600 dark:text-gray-400">
        {variant === "anonymous" ? t("trial.overBody") : t("wallet.outOfCreditsBodyAuthenticated")}
      </p>
      <div className="flex w-full flex-col gap-3">
        {variant === "anonymous" ? (
          <>
            <Link href="/register"><Button size="lg" className="w-full">{t("nav.register")}</Button></Link>
            <Link href="/login"><Button variant="outline" size="lg" className="w-full">{t("nav.login")}</Button></Link>
          </>
        ) : (
          <a href={`mailto:${SUPPORT_EMAIL}`}>
            <Button size="lg" className="w-full">
              <Mail className="h-4 w-4" />
              {t("help.contactButton")}
            </Button>
          </a>
        )}
      </div>
    </div>
  );
}

/** True if an api.ts call rejected with the server's out-of-credits error code. */
export function isOutOfCreditsError(err: unknown): boolean {
  return err instanceof Error && err.message === "OUT_OF_CREDITS";
}
