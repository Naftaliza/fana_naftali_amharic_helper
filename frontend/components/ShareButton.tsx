"use client";

import { Share2 } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";

/**
 * One-tap "Share Fana" growth loop. Shown after a successful analysis. Prefers the native
 * share sheet (mobile), and falls back to a WhatsApp deep link — the channel this community
 * already uses (same pattern as ReferralBlock's provider contact). The message is trilingual
 * and pre-filled with the app URL so recipients land straight on the upload experience.
 */
export function ShareButton({
  variant = "outline",
  className,
}: {
  variant?: "default" | "outline";
  className?: string;
}) {
  const { t } = useLanguage();

  const share = async () => {
    // Resolve the app URL at click time so it works across dev/prod without config.
    const url = window.location.origin;
    const text = t("share.message").replace("{url}", url);
    // Native share sheet where available (best mobile UX); tolerate user-cancel.
    if (navigator.share) {
      try {
        await navigator.share({ title: t("app.name"), text, url });
        return;
      } catch {
        return; // user dismissed the sheet — do nothing
      }
    }
    // Fallback: open WhatsApp with the pre-filled message.
    window.open(`https://wa.me/?text=${encodeURIComponent(text)}`, "_blank", "noopener");
  };

  return (
    <Button variant={variant} onClick={share} className={className}>
      <Share2 className="h-5 w-5" />
      {t("share.button")}
    </Button>
  );
}
