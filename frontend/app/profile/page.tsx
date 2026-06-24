"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { LANGUAGES } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function ProfilePage() {
  const { t } = useLanguage();
  const { user, loading, logout } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  if (!user) return <p className="text-gray-500">{t("common.loading")}</p>;

  const langLabel = LANGUAGES[user.preferredLanguage]?.label ?? "—";

  return (
    <div className="mx-auto max-w-md space-y-6">
      <h1 className="text-3xl font-bold">{t("nav.profile")}</h1>
      <Card>
        <CardHeader><CardTitle>{user.displayName}</CardTitle></CardHeader>
        <CardContent className="space-y-2 text-gray-700">
          <p><span className="text-gray-500">{t("auth.email")}:</span> {user.email}</p>
          <p><span className="text-gray-500">{t("nav.dashboard")}:</span> {langLabel}</p>
          <Button variant="outline" className="mt-4" onClick={logout}>{t("nav.logout")}</Button>
        </CardContent>
      </Card>
    </div>
  );
}
