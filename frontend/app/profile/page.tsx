"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { api } from "@/lib/api";
import { LANGUAGES } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function ProfilePage() {
  const { t } = useLanguage();
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const [exporting, setExporting] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  if (!user) return <p className="text-gray-500 dark:text-gray-400">{t("common.loading")}</p>;

  const langLabel = LANGUAGES[user.preferredLanguage]?.label ?? "—";

  const exportData = async () => {
    setError(null);
    setExporting(true);
    try {
      const data = await api.exportAccount();
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `fana-data-${user.email}.json`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 30_000);
    } catch {
      setError(t("profile.exportError"));
    } finally {
      setExporting(false);
    }
  };

  const deleteAccount = async () => {
    if (!window.confirm(t("profile.deleteAccountConfirm"))) return;
    setError(null);
    setDeleting(true);
    try {
      await api.deleteAccount();
      logout();
      router.push("/");
    } catch {
      setError(t("profile.deleteError"));
      setDeleting(false);
    }
  };

  return (
    <div className="mx-auto max-w-md space-y-6">
      <h1 className="text-3xl font-bold">{t("nav.profile")}</h1>
      <Card>
        <CardHeader><CardTitle>{user.displayName}</CardTitle></CardHeader>
        <CardContent className="space-y-2 text-gray-700 dark:text-gray-300">
          <p><span className="text-gray-500 dark:text-gray-400">{t("auth.email")}:</span> {user.email}</p>
          <p><span className="text-gray-500 dark:text-gray-400">{t("profile.language")}:</span> {langLabel}</p>
          <Button variant="outline" className="mt-4" onClick={logout}>{t("nav.logout")}</Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="space-y-3 py-4">
          <Button variant="outline" className="w-full" onClick={exportData} disabled={exporting}>
            {exporting ? t("profile.exporting") : t("profile.exportData")}
          </Button>
          <Button
            variant="outline"
            className="w-full border-red-200 text-red-600 hover:bg-red-50 dark:border-red-900 dark:hover:bg-red-950/40"
            onClick={deleteAccount}
            disabled={deleting}
          >
            {t("profile.deleteAccount")}
          </Button>
          {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        </CardContent>
      </Card>
    </div>
  );
}
