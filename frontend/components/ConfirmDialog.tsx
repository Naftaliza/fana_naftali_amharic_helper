"use client";

import { useLanguage } from "@/lib/language-context";
import { useFocusTrap } from "@/lib/useFocusTrap";
import { Button } from "@/components/ui/button";

/**
 * Accessible replacement for window.confirm(): translated, RTL-aware, and — critically — the
 * confirm button always carries a real verb ("Delete document") instead of the browser/OS
 * locale's untranslated "OK", which a user whose device locale differs from their chosen
 * app language may not be able to read. Copies the modal shell already proven in Onboarding.tsx.
 */
export function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  destructive = false,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  body: string;
  confirmLabel: string;
  destructive?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { t, rtl } = useLanguage();
  const trapRef = useFocusTrap(open, onCancel);

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
    >
      <div ref={trapRef} dir={rtl ? "rtl" : "ltr"} className="relative w-full max-w-sm rounded-3xl bg-white p-6 text-center shadow-soft dark:bg-gray-900">
        <h2 className="mb-2 text-xl font-bold">{title}</h2>
        <p className="mb-6 text-gray-600 dark:text-gray-400">{body}</p>
        <div className="flex gap-3">
          <Button variant="outline" className="flex-1" onClick={onCancel}>
            {t("confirm.cancel")}
          </Button>
          <Button
            className={destructive ? "flex-1 bg-red-600 hover:bg-red-700 dark:bg-red-700 dark:hover:bg-red-600" : "flex-1"}
            onClick={onConfirm}
          >
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
