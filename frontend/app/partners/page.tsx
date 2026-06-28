"use client";

import { useState } from "react";
import { CheckCircle2, Users, BadgeDollarSign, MessageSquare } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { DOCUMENT_CATEGORY } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

// Category options for the dropdown, by i18n key (values mirror DOCUMENT_CATEGORY).
const CATEGORY_KEYS: { value: number; key: string }[] = [
  { value: DOCUMENT_CATEGORY.Government, key: "cat.government" },
  { value: DOCUMENT_CATEGORY.Bank, key: "cat.bank" },
  { value: DOCUMENT_CATEGORY.Insurance, key: "cat.insurance" },
  { value: DOCUMENT_CATEGORY.Employment, key: "cat.employment" },
  { value: DOCUMENT_CATEGORY.Healthcare, key: "cat.healthcare" },
  { value: DOCUMENT_CATEGORY.Municipality, key: "cat.municipality" },
  { value: DOCUMENT_CATEGORY.Other, key: "cat.other" },
];

export default function PartnersPage() {
  const { t, rtl } = useLanguage();
  const dir = rtl ? "rtl" : "ltr";

  const [form, setForm] = useState({
    displayName: "",
    category: DOCUMENT_CATEGORY.Government,
    city: "",
    phone: "",
    whatsApp: "",
    contactEmail: "",
    description: "",
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const set = (k: keyof typeof form, v: string | number) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api.applyAsProvider({ ...form, category: Number(form.category) });
      setDone(true);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (done) {
    return (
      <div className="mx-auto flex max-w-md flex-col items-center gap-5 pt-16 text-center" dir={dir}>
        <CheckCircle2 className="h-16 w-16 text-brand" />
        <h1 className="text-2xl font-bold">{t("partners.thanksTitle")}</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">{t("partners.thanksBody")}</p>
      </div>
    );
  }

  const perk = (Icon: typeof Users, title: string, body: string) => (
    <div className="flex items-start gap-3">
      <Icon className="mt-0.5 h-6 w-6 shrink-0 text-brand" />
      <div>
        <p className="font-semibold">{title}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{body}</p>
      </div>
    </div>
  );

  return (
    <div className="mx-auto max-w-2xl space-y-8 pt-6" dir={dir}>
      <header className="space-y-3 text-center">
        <h1 className="bg-brand-gradient bg-clip-text text-3xl font-bold leading-tight text-transparent sm:text-4xl">
          {t("partners.headline")}
        </h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">{t("partners.subheadline")}</p>
      </header>

      <div className="grid gap-5 sm:grid-cols-3">
        {perk(Users, t("partners.perk1Title"), t("partners.perk1Body"))}
        {perk(BadgeDollarSign, t("partners.perk2Title"), t("partners.perk2Body"))}
        {perk(MessageSquare, t("partners.perk3Title"), t("partners.perk3Body"))}
      </div>

      <Card>
        <CardHeader><CardTitle>{t("partners.formTitle")}</CardTitle></CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-4">
            <Input
              aria-label={t("partners.businessName")}
              placeholder={t("partners.businessName")}
              value={form.displayName}
              onChange={(e) => set("displayName", e.target.value)}
              required
            />
            <select
              aria-label={t("partners.category")}
              value={form.category}
              onChange={(e) => set("category", Number(e.target.value))}
              className="h-11 w-full rounded-xl border border-gray-300 bg-white px-4 text-base focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
            >
              {CATEGORY_KEYS.map((c) => (
                <option key={c.value} value={c.value}>{t(c.key)}</option>
              ))}
            </select>
            <Input
              aria-label={t("partners.city")}
              placeholder={t("partners.city")}
              value={form.city}
              onChange={(e) => set("city", e.target.value)}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                aria-label={t("partners.phone")}
                placeholder={t("partners.phone")}
                value={form.phone}
                onChange={(e) => set("phone", e.target.value)}
              />
              <Input
                aria-label={t("partners.whatsapp")}
                placeholder={t("partners.whatsapp")}
                value={form.whatsApp}
                onChange={(e) => set("whatsApp", e.target.value)}
              />
            </div>
            <Input
              type="email"
              aria-label={t("partners.email")}
              placeholder={t("partners.email")}
              value={form.contactEmail}
              onChange={(e) => set("contactEmail", e.target.value)}
              required
            />
            <textarea
              aria-label={t("partners.description")}
              placeholder={t("partners.descriptionHint")}
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              rows={3}
              required
              className="w-full rounded-xl border border-gray-300 bg-white px-4 py-2 text-base focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand placeholder:text-gray-400 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100 dark:placeholder:text-gray-500"
            />
            {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
            <Button type="submit" className="w-full" disabled={busy}>
              {busy ? t("common.loading") : t("partners.submit")}
            </Button>
            <p className="text-center text-xs text-gray-500 dark:text-gray-400">{t("partners.reviewNote")}</p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
