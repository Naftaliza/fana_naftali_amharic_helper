"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { dictionaries } from "@/i18n/dictionaries";
import { isRtl, type Language } from "@/lib/types";

interface LanguageContextValue {
  language: Language;
  setLanguage: (lang: Language) => void;
  t: (key: string) => string;
  rtl: boolean;
  // False until the user has explicitly picked a language (via LanguageGate) or one was
  // already stored from a prior visit — distinct from `language` itself, which always has
  // a value (defaulting to Hebrew), so components can tell "chosen" apart from "guessed".
  languageChosen: boolean;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  // Hebrew is the default language.
  const [language, setLanguageState] = useState<Language>("he");
  const [languageChosen, setLanguageChosen] = useState(false);

  useEffect(() => {
    const stored = window.localStorage.getItem("lang") as Language | null;
    if (stored) {
      setLanguageState(stored);
      setLanguageChosen(true);
    }
  }, []);

  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = isRtl(language) ? "rtl" : "ltr";
  }, [language]);

  const setLanguage = (lang: Language) => {
    window.localStorage.setItem("lang", lang);
    setLanguageState(lang);
    setLanguageChosen(true);
  };

  const t = (key: string) => dictionaries[language][key] ?? key;

  return (
    <LanguageContext.Provider value={{ language, setLanguage, t, rtl: isRtl(language), languageChosen }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  const ctx = useContext(LanguageContext);
  if (!ctx) throw new Error("useLanguage must be used within LanguageProvider");
  return ctx;
}
