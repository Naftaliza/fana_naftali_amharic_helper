"use client";

import { useEffect, useState } from "react";

/**
 * Tracks browser connectivity via the online/offline events. Starts from navigator.onLine so a
 * page loaded while already offline (the service worker's cached app shell still renders fine,
 * see public/sw.js) reflects that immediately rather than assuming online for one render.
 */
export function useOnlineStatus(): boolean {
  const [online, setOnline] = useState(() => (typeof navigator === "undefined" ? true : navigator.onLine));

  useEffect(() => {
    const goOnline = () => setOnline(true);
    const goOffline = () => setOnline(false);
    window.addEventListener("online", goOnline);
    window.addEventListener("offline", goOffline);
    return () => {
      window.removeEventListener("online", goOnline);
      window.removeEventListener("offline", goOffline);
    };
  }, []);

  return online;
}
