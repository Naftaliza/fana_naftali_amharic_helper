"use client";

import { useEffect, useState } from "react";
import { Camera, HandHelping, Languages, X } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { useAuth } from "@/lib/auth-context";
import { hasSeenOnboarding, markOnboardingSeen } from "@/lib/onboarding";
import { useOnboarding } from "@/lib/onboarding-context";
import { useFocusTrap } from "@/lib/useFocusTrap";
import { Button } from "@/components/ui/button";

const STEPS = [
  { icon: Camera, titleKey: "onboarding.step1Title", bodyKey: "onboarding.step1Body" },
  { icon: Languages, titleKey: "onboarding.step2Title", bodyKey: "onboarding.step2Body" },
  { icon: HandHelping, titleKey: "onboarding.step3Title", bodyKey: "onboarding.step3Body" },
] as const;

/**
 * 3-step walkthrough shown to new anonymous visitors before the uploader, so the value prop
 * (photograph -> understand -> get expert help) lands before the trial counter starts ticking.
 * Auto-shows once per visitor (localStorage flag, same pattern as lib/trial.ts), but can also be
 * re-opened any time — by anyone, signed in or not — via useOnboarding().show() (see the Help
 * page), since a first glance isn't always enough and there was previously no way back in.
 */
export function Onboarding() {
  const { t, rtl, languageChosen } = useLanguage();
  const { user, loading } = useAuth();
  const { open, show, hide } = useOnboarding();
  const [step, setStep] = useState(0);

  useEffect(() => {
    // Wait for the language gate to be resolved so onboarding never renders in the wrong
    // language behind it. Auto-show stays anonymous-only — it's timed to land before the trial
    // counter starts ticking, which doesn't apply to a signed-in user.
    if (!loading && !user && languageChosen && !hasSeenOnboarding()) show();
  }, [loading, user, languageChosen, show]);

  // Always start from step 1 whenever the dialog (re-)opens, whether that's the automatic
  // first-run show or a manual replay from the Help page.
  useEffect(() => {
    if (open) setStep(0);
  }, [open]);

  const close = () => {
    markOnboardingSeen();
    hide();
  };

  const trapRef = useFocusTrap(open, close);

  if (!open) return null;

  const { icon: Icon, titleKey, bodyKey } = STEPS[step];
  const isLast = step === STEPS.length - 1;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t(titleKey)}
      data-a11y-fixed
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
    >
      <div ref={trapRef} dir={rtl ? "rtl" : "ltr"} className="relative w-full max-w-sm rounded-3xl bg-white p-6 text-center shadow-soft dark:bg-gray-900">
        <button
          onClick={close}
          aria-label={t("onboarding.skip")}
          className="absolute end-4 top-4 rounded-lg p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
        >
          <X className="h-5 w-5" />
        </button>

        <div className="mx-auto mb-5 grid h-20 w-20 place-items-center rounded-full bg-brand-gradient text-white">
          <Icon className="h-10 w-10" />
        </div>

        <p className="mb-2 text-sm text-gray-500 dark:text-gray-400">
          {t("onboarding.stepOf").replace("{current}", String(step + 1)).replace("{total}", String(STEPS.length))}
        </p>
        <h2 className="mb-2 text-xl font-bold">{t(titleKey)}</h2>
        <p className="mb-6 text-gray-600 dark:text-gray-400">{t(bodyKey)}</p>

        <div className="mb-5 flex justify-center gap-2">
          {STEPS.map((_, i) => (
            <span key={i} className={`h-2 w-2 rounded-full ${i === step ? "bg-brand" : "bg-gray-200 dark:bg-gray-700"}`} />
          ))}
        </div>

        <div className="flex gap-3">
          {!isLast && (
            <Button variant="outline" className="flex-1" onClick={close}>
              {t("onboarding.skip")}
            </Button>
          )}
          <Button className="flex-1" onClick={() => (isLast ? close() : setStep((s) => s + 1))}>
            {isLast ? t("onboarding.start") : t("onboarding.next")}
          </Button>
        </div>
      </div>
    </div>
  );
}
