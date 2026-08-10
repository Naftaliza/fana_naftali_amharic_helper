"use client";

import Link from "next/link";
import { ArrowLeft, ArrowRight } from "lucide-react";
import { useLanguage } from "@/lib/language-context";

/**
 * Direction-aware "back" affordance — generalizes the pattern already written twice
 * (documents/[id]/page.tsx, admin/organizations/page.tsx) so pages that were missing any way
 * back at all (wallet, gift, terms, privacy, partners) get the same one, instead of a bespoke
 * variant each. `label` defaults to a generic "Back"; pass a specific one (e.g. "Back to
 * document") where the destination benefits from being named.
 */
export function BackLink({ href, label }: { href: string; label?: string }) {
  const { t, rtl } = useLanguage();
  const Icon = rtl ? ArrowRight : ArrowLeft;

  return (
    <Link
      href={href}
      className="mb-4 inline-flex items-center gap-2 text-sm font-medium text-gray-500 hover:text-brand dark:text-gray-400"
    >
      <Icon className="h-4 w-4" />
      {label ?? t("common.back")}
    </Link>
  );
}
