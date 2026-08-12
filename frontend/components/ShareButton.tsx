"use client";

import { Share2 } from "lucide-react";
import { useLanguage } from "@/lib/language-context";

/**
 * One-tap "Share Fana" growth loop. Styled as a small badge, not a full-width Button — a much
 * smaller footprint on a page whose one job is the upload button above it. Prefers the native
 * share sheet on mobile, which lets the person pick WhatsApp, SMS, email, or whatever they
 * actually use — it is NOT WhatsApp-specific, so the icon/color must stay generic rather than
 * badging one particular channel. Falls back to a direct WhatsApp deep link only where
 * navigator.share doesn't exist at all (desktop browsers). The message is trilingual and
 * pre-filled with the app URL.
 */
export function ShareButton({ className }: { className?: string }) {
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
    <button
      type="button"
      onClick={share}
      className={`inline-flex items-center gap-1.5 rounded-full bg-brand px-3.5 py-1.5 text-sm font-medium text-white shadow-soft transition-transform hover:scale-105 active:scale-100 ${className ?? ""}`}
    >
      <Share2 className="h-4 w-4" />
      {t("share.button")}
    </button>
  );
}
