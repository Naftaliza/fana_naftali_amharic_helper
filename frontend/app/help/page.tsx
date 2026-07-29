"use client";

import { HelpCircle, Mail, RotateCcw } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { useOnboarding } from "@/lib/onboarding-context";
import { SUPPORT_EMAIL } from "@/lib/support";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const FAQ_KEYS: { q: string; a: string }[] = [
  { q: "help.faqQ1", a: "help.faqA1" },
  { q: "help.faqQ2", a: "help.faqA2" },
  { q: "help.faqQ3", a: "help.faqA3" },
];

export default function HelpPage() {
  const { t, rtl } = useLanguage();
  const { show } = useOnboarding();
  const dir = rtl ? "rtl" : "ltr";

  return (
    <div className="mx-auto max-w-2xl pt-6" dir={dir}>
      <div className="mb-6 flex items-center gap-3">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-gradient text-white">
          <HelpCircle className="h-6 w-6" />
        </span>
        <div>
          <h1 className="text-xl font-bold">{t("help.title")}</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("help.intro")}</p>
        </div>
      </div>

      <div className="grid gap-4">
        <Card>
          <CardHeader><CardTitle className="text-lg">{t("help.walkthroughTitle")}</CardTitle></CardHeader>
          <CardContent>
            <Button variant="outline" onClick={show}>
              <RotateCcw className="h-4 w-4" />
              {t("help.replayWalkthrough")}
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-lg">{t("help.faqTitle")}</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            {FAQ_KEYS.map(({ q, a }) => (
              <div key={q}>
                <p className="font-medium text-gray-900 dark:text-gray-100">{t(q)}</p>
                <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t(a)}</p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-lg">{t("help.contactTitle")}</CardTitle></CardHeader>
          <CardContent className="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-gray-700 dark:text-gray-300">{t("help.contactBody")}</p>
            <a href={`mailto:${SUPPORT_EMAIL}`} className="shrink-0">
              <Button variant="outline">
                <Mail className="h-4 w-4" />
                {t("help.contactButton")}
              </Button>
            </a>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
