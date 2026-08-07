"use client";

import Link from "next/link";
import { ShieldCheck, Sparkles, HelpCircle, Briefcase } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { useAuth } from "@/lib/auth-context";
import { useOrganization } from "@/lib/organization-context";
import { ShareButton } from "@/components/ShareButton";

/**
 * Below-the-fold content on `/`: trust signals, the growth-loop share button, and links to
 * /help and /partners. Hidden for a tenant-branded visitor (their own institution's welcome
 * message leads instead — see UploadExperience's `organization` branch) and for a signed-in
 * user (they already know the product; this is a first-impression aid).
 */
export function HomeExplainer() {
  const { t } = useLanguage();
  const { user } = useAuth();
  const { organization } = useOrganization();

  if (organization || user) return null;

  return (
    <div className="mx-auto mt-10 flex max-w-xl flex-col items-center gap-6 text-center">
      <div className="flex flex-wrap justify-center gap-4 text-sm text-gray-500 dark:text-gray-400">
        <span className="flex items-center gap-1.5">
          <Sparkles className="h-4 w-4 text-brand" />
          {t("home.trust1")}
        </span>
        <span className="flex items-center gap-1.5">
          <ShieldCheck className="h-4 w-4 text-brand" />
          {t("home.trust2")}
        </span>
      </div>

      <ShareButton />

      <div className="flex flex-wrap justify-center gap-4 text-sm text-gray-500 dark:text-gray-400">
        <Link href="/help" className="flex items-center gap-1.5 hover:text-brand hover:underline">
          <HelpCircle className="h-4 w-4" />
          {t("nav.help")}
        </Link>
        <Link href="/partners" className="flex items-center gap-1.5 hover:text-brand hover:underline">
          <Briefcase className="h-4 w-4" />
          {t("nav.partners")}
        </Link>
      </div>
    </div>
  );
}
