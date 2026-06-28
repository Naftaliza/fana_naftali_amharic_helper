"use client";

import { useEffect, useState } from "react";
import { HandHelping, MessageCircle, Phone } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { isRtl, loc, URGENCY_ENUM, type AnalysisResult, type Provider } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Sponsored-referral block shown under an analysis. Matches vetted Amharic-speaking
 * professionals to the document's category, and logs a lead when the user makes contact.
 * Shown only for Medium+ urgency (so it feels helpful, not spammy) and only when providers
 * actually match — otherwise it renders nothing.
 */
export function ReferralBlock({
  analysis,
  documentId,
}: {
  analysis: AnalysisResult;
  documentId?: string;
}) {
  const { t, language } = useLanguage();
  const dir = isRtl(language) ? "rtl" : "ltr";
  const [providers, setProviders] = useState<Provider[]>([]);

  const category = analysis.category;
  // The API serializes enums as ints on the wire, but the type says string — tolerate both.
  const urgency =
    typeof analysis.urgencyLevel === "number"
      ? analysis.urgencyLevel
      : URGENCY_ENUM[analysis.urgencyLevel] ?? 0;

  useEffect(() => {
    // Only surface help for documents that need action (Medium+). Skip if uncategorized.
    if (category === undefined || urgency < 1) {
      setProviders([]);
      return;
    }
    let cancelled = false;
    api
      .getReferrals(category)
      .then((list) => { if (!cancelled) setProviders(list); })
      .catch(() => { if (!cancelled) setProviders([]); }); // referrals are optional — fail silent
    return () => { cancelled = true; };
  }, [category, urgency]);

  if (providers.length === 0) return null;

  const contact = (p: Provider) => {
    // Short ref code shared between the logged lead and the WhatsApp message, so the provider
    // can quote it and it can be matched on the admin Leads tab.
    const ref = (Date.now().toString(36).slice(-4) + Math.random().toString(36).slice(2, 4)).toUpperCase();
    // Log the lead (best-effort), then open the user's preferred channel.
    api.logLead(p.id, { category: category!, urgency, documentId, ref }).catch(() => {});
    if (p.whatsApp) {
      const text = encodeURIComponent(t("referral.waMessage").replace("{ref}", ref));
      window.open(`https://wa.me/${p.whatsApp}?text=${text}`, "_blank", "noopener");
    } else if (p.phone) {
      window.location.href = `tel:${p.phone}`;
    }
  };

  return (
    <Card className="border-brand bg-brand-light dark:bg-brand/15">
      <CardContent className="py-5">
        <div className="mb-3 flex items-center gap-2" dir={dir}>
          <HandHelping className="h-5 w-5 shrink-0 text-brand" />
          <h3 className="text-lg font-semibold">{t("referral.title")}</h3>
        </div>

        <ul className="grid gap-3" dir={dir}>
          {providers.map((p) => (
            <li
              key={p.id}
              className="flex flex-col gap-2 rounded-2xl border border-gray-100 bg-white/90 p-3 shadow-soft dark:border-gray-700 dark:bg-gray-800/90 sm:flex-row sm:items-center sm:justify-between"
            >
              <div>
                <p className="font-medium">
                  {p.displayName}
                  {p.city && <span className="text-gray-500 dark:text-gray-400"> · {p.city}</span>}
                </p>
                <p className="text-sm text-gray-600 dark:text-gray-300">{loc(p.blurb, language)}</p>
              </div>
              <button
                onClick={() => contact(p)}
                className="inline-flex shrink-0 items-center justify-center gap-2 rounded-full bg-brand px-4 py-2 text-sm font-medium text-white hover:bg-brand-dark"
              >
                {p.whatsApp ? <MessageCircle className="h-4 w-4" /> : <Phone className="h-4 w-4" />}
                {p.whatsApp ? t("referral.whatsapp") : t("referral.call")}
              </button>
            </li>
          ))}
        </ul>

        <p className="mt-3 text-xs text-gray-500 dark:text-gray-400" dir={dir}>
          {t("referral.disclosure")}
        </p>
      </CardContent>
    </Card>
  );
}
