"use client";

import { useEffect, useState } from "react";
import { FileText } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { LANGUAGE_ENUM } from "@/lib/types";
import { BackLink } from "@/components/BackLink";

export default function TermsPage() {
  const { t, language, rtl } = useLanguage();
  const [body, setBody] = useState<string | null>(null);
  const [error, setError] = useState(false);
  const dir = rtl ? "rtl" : "ltr";

  useEffect(() => {
    let cancelled = false;
    api.getLegalDocument("terms", LANGUAGE_ENUM[language])
      .then((doc) => { if (!cancelled) setBody(doc.body); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [language]);

  return (
    <div className="mx-auto max-w-2xl pt-6" dir={dir}>
      <BackLink href="/" />
      <div className="mb-6 flex items-center gap-3">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-gradient text-white">
          <FileText className="h-6 w-6" />
        </span>
        <h1 className="text-xl font-bold">{t("legal.termsTitle")}</h1>
      </div>

      {error && <p role="alert" className="text-red-600">{t("legal.loadError")}</p>}
      {!error && !body && <p className="text-gray-500 dark:text-gray-400">{t("legal.loading")}</p>}
      {body && (
        <div className="whitespace-pre-line rounded-2xl border border-gray-100 bg-white p-6 text-gray-700 shadow-soft dark:border-gray-800 dark:bg-gray-900 dark:text-gray-300">
          {body}
        </div>
      )}
    </div>
  );
}
