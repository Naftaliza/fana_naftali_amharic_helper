"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/**
 * Redirects home when a feature flag (see lib/featureFlags.ts) is off — for a page that should
 * be entirely unreachable while its flag is disabled, not just visually hidden. Returns the flag
 * value so the caller can bail out of rendering immediately:
 *
 *   const enabled = useFeatureGate(FEATURE_WALLET);
 *   if (!enabled) return null;
 */
export function useFeatureGate(enabled: boolean): boolean {
  const router = useRouter();
  useEffect(() => {
    if (!enabled) router.replace("/");
  }, [enabled, router]);
  return enabled;
}
