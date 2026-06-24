"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { FileText, Plus, Trash2 } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { useAuth } from "@/lib/auth-context";
import { useRouter } from "next/navigation";
import type { DocumentSummary } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export default function DashboardPage() {
  const { t } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [docs, setDocs] = useState<DocumentSummary[]>([]);
  const [fetching, setFetching] = useState(true);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.preventDefault(); // don't navigate into the document
    if (!window.confirm(t("doc.confirmDelete"))) return;
    setDeletingId(id);
    try {
      await api.deleteDocument(id);
      setDocs((list) => list.filter((d) => d.id !== id));
    } catch {
      // leave the row in place if the delete failed
    } finally {
      setDeletingId(null);
    }
  };

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  useEffect(() => {
    if (user) api.listDocuments().then(setDocs).catch(() => {}).finally(() => setFetching(false));
  }, [user]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">{t("nav.dashboard")}</h1>
        <Link href="/upload"><Button><Plus className="h-5 w-5" />{t("nav.upload")}</Button></Link>
      </div>

      {fetching ? (
        <p className="text-gray-500">{t("common.loading")}</p>
      ) : docs.length === 0 ? (
        <Card><CardContent className="py-12 text-center text-gray-500">
          {t("upload.drop")}
        </CardContent></Card>
      ) : (
        <div className="grid gap-3">
          {docs.map((d) => (
            <Card key={d.id} className="transition-shadow hover:shadow-md">
              <CardContent className="flex items-center gap-4 py-4">
                <Link href={`/documents/${d.id}`} className="flex min-w-0 flex-1 items-center gap-4">
                  <FileText className="h-8 w-8 shrink-0 text-brand" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{d.fileName}</p>
                    <p className="text-sm text-gray-500">{new Date(d.uploadedAt).toLocaleString()}</p>
                  </div>
                  {d.hasAnalysis && <span className="rounded-full bg-green-100 px-3 py-1 text-xs text-green-800">✓</span>}
                </Link>
                <button
                  onClick={(e) => handleDelete(d.id, e)}
                  disabled={deletingId === d.id}
                  aria-label={t("doc.delete")}
                  title={t("doc.delete")}
                  className="shrink-0 rounded-lg p-2 text-gray-400 transition-colors hover:bg-red-50 hover:text-red-600 disabled:opacity-50"
                >
                  <Trash2 className="h-5 w-5" />
                </button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
