"use client";

import { Camera, Languages, Volume2 } from "lucide-react";
import { useLanguage } from "@/lib/language-context";

// Reuses Onboarding.tsx's exact three-step story (photograph -> understand -> get help) so the
// messaging is consistent whether a visitor reads the walkthrough or just this strip. Sits above
// the camera button on `/`, not below it — the single strongest thing about this product is that
// a non-reader reaches value in one tap, so nothing here should compete with that tap for
// attention; three icons and short captions read in under two seconds either way.
const STEPS = [
  { icon: Camera, key: "onboarding.step1Title" },
  { icon: Languages, key: "onboarding.step2Title" },
  { icon: Volume2, key: "onboarding.step3Title" },
] as const;

export function HowItWorksStrip() {
  const { t } = useLanguage();

  return (
    <div className="grid w-full grid-cols-3 gap-2 text-center">
      {STEPS.map(({ icon: Icon, key }) => (
        <div key={key} className="flex flex-col items-center gap-1.5">
          <div className="grid h-12 w-12 place-items-center rounded-2xl bg-brand-light text-brand dark:bg-brand/15">
            <Icon className="h-6 w-6" />
          </div>
          <p className="text-xs leading-tight text-gray-600 dark:text-gray-400">{t(key)}</p>
        </div>
      ))}
    </div>
  );
}
