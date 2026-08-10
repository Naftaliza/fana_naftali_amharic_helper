"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";

/**
 * Client-side navigation is silent for a screen-reader user: the URL and content change, but
 * nothing moves focus and nothing is announced (SkipLink only helps with the very first tab
 * press). On every route change, move focus to the new page's own <h1> — reusing whatever title
 * each page already renders rather than a separate route→title map that would drift from it —
 * and echo that title into a polite live region so it's announced even for the (common, since
 * these are mostly full h1 headings, not always the initial focus target) case where focus
 * moving alone isn't read.
 */
export function RouteAnnouncer() {
  const pathname = usePathname();
  const [announcement, setAnnouncement] = useState("");
  const firstRender = useRef(true);

  useEffect(() => {
    // Skip the very first mount — the browser already announces the initial page load.
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    const heading = document.querySelector<HTMLElement>("#main h1");
    if (!heading) return;

    const text = heading.textContent?.trim();
    if (text) setAnnouncement(text);

    if (!heading.hasAttribute("tabindex")) heading.setAttribute("tabindex", "-1");
    heading.focus();
  }, [pathname]);

  return (
    <div role="status" aria-live="polite" className="sr-only">
      {announcement}
    </div>
  );
}
