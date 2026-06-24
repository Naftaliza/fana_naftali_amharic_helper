"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { dictionaries } from "@/i18n/dictionaries";
import { isRtl, type Language } from "@/lib/types";

interface LanguageContextValue {
  language: Language;
  setLanguage: (lang: Language) => void;
  t: (key: string) => string;
  rtl: boolean;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  // Hebrew is the default language.
  const [language, setLanguageState] = useState<Language>("he");

  useEffect(() => {
    const stored = window.localStorage.getItem("lang") as Language | null;
    if (stored) setLanguageState(stored);
  }, []);

  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = isRtl(language) ? "rtl" : "ltr";
  }, [language]);

  const setLanguage = (lang: Language) => {
    window.localStorage.setItem("lang", lang);
    setLanguageState(lang);
  };

  const t = (key: string) => dictionaries[language][key] ?? key;

  return (
    <LanguageContext.Provider value={{ language, setLanguage, t, rtl: isRtl(language) }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  const ctx = useContext(LanguageContext);
  if (!ctx) throw new Error("useLanguage must be used within LanguageProvider");
  return ctx;
}
