"use client";

import { WifiOff } from "lucide-react";
import { useOnlineStatus } from "@/lib/useOnlineStatus";
import { useLanguage } from "@/lib/language-context";

/**
 * Global connectivity banner. The service worker's cached app shell (public/sw.js) means the app
 * still loads and looks normal with no connection — this is the one place that says otherwise,
 * so a user isn't left guessing why every action is quietly failing.
 */
export function OfflineBanner() {
  const online = useOnlineStatus();
  const { t, rtl } = useLanguage();

  if (online) return null;

  return (
    <div
      role="status"
      dir={rtl ? "rtl" : "ltr"}
      className="flex items-center justify-center gap-2 bg-amber-500 px-4 py-2 text-center text-sm font-medium text-white"
    >
      <WifiOff className="h-4 w-4 shrink-0" />
      {t("offline.banner")}
    </div>
  );
}
