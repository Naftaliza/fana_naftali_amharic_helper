"use client";

import { useLanguage } from "@/lib/language-context";

/** Keyboard/screen-reader users can jump straight to the main content. */
export function SkipLink() {
  const { t } = useLanguage();
  return (
    <a href="#main" data-print-hide className="skip-link">
      {t("a11y.skip")}
    </a>
  );
}
