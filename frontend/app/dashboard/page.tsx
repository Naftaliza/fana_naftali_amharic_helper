"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { FileText, Plus, Trash2 } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { useAuth } from "@/lib/auth-context";
import { useRouter } from "next/navigation";
import { DOCUMENT_STATUS, type DocumentSummary } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ComingUpStrip } from "@/components/ComingUpStrip";

export default function DashboardPage() {
  const { t } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [docs, setDocs] = useState<DocumentSummary[]>([]);
  const [fetching, setFetching] = useState(true);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState(false);

  const requestDelete = (id: string, e: React.MouseEvent) => {
    e.preventDefault(); // don't navigate into the document
    setDeleteError(null);
    setConfirmId(id);
  };

  const confirmDelete = async () => {
    const id = confirmId!;
    setConfirmId(null);
    setDeletingId(id);
    try {
      await api.deleteDocument(id);
      setDocs((list) => list.filter((d) => d.id !== id));
    } catch {
      setDeleteError(t("doc.deleteError"));
    } finally {
      setDeletingId(null);
    }
  };

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  const reload = () => {
    setFetching(true);
    setLoadError(false);
    api.listDocuments()
      .then(setDocs)
      .catch(() => setLoadError(true))
      .finally(() => setFetching(false));
  };

  useEffect(() => {
    if (user) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl font-bold">{t("nav.dashboard")}</h1>
        <Link href="/upload"><Button className="whitespace-nowrap"><Plus className="h-5 w-5" />{t("nav.upload")}</Button></Link>
      </div>

      <ComingUpStrip docs={docs} />

      {fetching ? (
        <p className="text-gray-500">{t("common.loading")}</p>
      ) : loadError ? (
        <Card><CardContent className="flex flex-col items-center gap-3 py-12 text-center">
          <p role="alert" className="text-red-600">{t("offline.dashboardError")}</p>
          <Button variant="outline" onClick={reload}>{t("offline.retry")}</Button>
        </CardContent></Card>
      ) : docs.length === 0 ? (
        <Card><CardContent className="py-12 text-center text-gray-500 dark:text-gray-400">
          {t("upload.drop")}
        </CardContent></Card>
      ) : (
        <div className="grid gap-3">
          {docs.map((d) => (
            <Card key={d.id} className="min-w-0 transition-shadow hover:shadow-md">
              <CardContent className="flex items-center gap-4 py-4">
                <Link href={`/documents/${d.id}`} className="flex min-w-0 flex-1 items-center gap-4">
                  <FileText className="h-8 w-8 shrink-0 text-brand" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{d.fileName}</p>
                    <p className="text-sm text-gray-500 dark:text-gray-400">{new Date(d.uploadedAt).toLocaleString()}</p>
                  </div>
                  {d.status === DOCUMENT_STATUS.Pending || d.status === DOCUMENT_STATUS.Processing ? (
                    <span className="shrink-0 rounded-full bg-blue-100 px-3 py-1 text-xs text-blue-800 dark:bg-blue-900/40 dark:text-blue-300">
                      {t("doc.processingBadge")}
                    </span>
                  ) : d.status === DOCUMENT_STATUS.Failed ? (
                    <span className="shrink-0 rounded-full bg-red-100 px-3 py-1 text-xs text-red-800 dark:bg-red-900/40 dark:text-red-300">
                      {t("doc.failedBadge")}
                    </span>
                  ) : (
                    d.hasAnalysis && <span className="shrink-0 rounded-full bg-green-100 px-3 py-1 text-xs text-green-800 dark:bg-green-900/40 dark:text-green-300">✓</span>
                  )}
                </Link>
                <button
                  onClick={(e) => requestDelete(d.id, e)}
                  disabled={deletingId === d.id}
                  aria-label={t("doc.delete")}
                  title={t("doc.delete")}
                  className="shrink-0 rounded-lg p-2 text-gray-400 transition-colors hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40 disabled:opacity-50"
                >
                  <Trash2 className="h-5 w-5" />
                </button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {deleteError && <p role="alert" className="text-sm text-red-600">{deleteError}</p>}

      <ConfirmDialog
        open={confirmId !== null}
        title={t("doc.delete")}
        body={t("doc.confirmDelete")}
        confirmLabel={t("doc.delete")}
        destructive
        onConfirm={confirmDelete}
        onCancel={() => setConfirmId(null)}
      />
    </div>
  );
}
