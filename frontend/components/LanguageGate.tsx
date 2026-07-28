"use client";

import { useLanguage } from "@/lib/language-context";
import { LANGUAGES } from "@/lib/types";
import { Flag } from "@/components/ui/Flag";

/**
 * Full-screen, blocking language picker shown before anything else on a visitor's first
 * visit (no stored `lang`). The app otherwise defaults to Hebrew, which is unreadable to
 * the Amharic-speaking users it's built for — silently guessing from navigator.language
 * is avoided on purpose (cheap Android phones in Israel often report "he" regardless of
 * the owner's language), so this asks once, in every script at once, and remembers it.
 */
export function LanguageGate() {
  const { languageChosen, setLanguage } = useLanguage();

  if (languageChosen) return null;

  return (
    <div
      data-language-gate
      role="dialog"
      aria-modal="true"
      aria-label="בחרו שפה · ቋንቋ ይምረጡ · Choose your language"
      className="fixed inset-0 z-[60] flex items-center justify-center bg-white p-6 dark:bg-gray-900"
    >
      <div className="w-full max-w-sm text-center">
        <h1 className="mb-8 text-xl font-bold text-gray-900 dark:text-gray-100">
          בחרו שפה · ቋንቋ ይምረጡ · Choose your language
        </h1>
        <div className="flex flex-col gap-3">
          {LANGUAGES.map((l) => (
            <button
              key={l.code}
              onClick={() => setLanguage(l.code)}
              className="flex min-h-[72px] items-center gap-4 rounded-2xl border border-gray-200 bg-white px-6 py-4 text-start shadow-soft transition-transform hover:scale-[1.02] active:scale-100 dark:border-gray-700 dark:bg-gray-800"
            >
              <Flag code={l.flag} size={32} />
              <span className="text-2xl font-bold text-gray-900 dark:text-gray-100">{l.label}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
