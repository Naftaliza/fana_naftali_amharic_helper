"use client";

import { useEffect } from "react";

/** Registers the PWA service worker so the app is installable / works as an app shell. */
export function ServiceWorker() {
  useEffect(() => {
    if ("serviceWorker" in navigator) {
      navigator.serviceWorker.register("/sw.js").catch(() => {
        // Registration failures are non-fatal; the app still works as a normal site.
      });
    }
  }, []);
  return null;
}
