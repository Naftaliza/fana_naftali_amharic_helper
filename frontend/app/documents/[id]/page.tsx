"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, ArrowRight, MessageCircle, Sparkles, X } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { DOCUMENT_STATUS, type DocumentDetail } from "@/lib/types";
import { AnalysisCard } from "@/components/AnalysisCard";
import { AnalyzingState } from "@/components/AnalyzingState";
import { DocumentPages } from "@/components/DocumentPages";
import { ShareButton } from "@/components/ShareButton";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ConfirmDialog";

// How often to poll GET /documents/{id} while the background OCR pipeline (DocumentProcessor)
// is still working through the document's pages.
const POLL_INTERVAL_MS = 1500;

export default function DocumentDetailPage() {
  const { t, rtl } = useLanguage();
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const [doc, setDoc] = useState<DocumentDetail | null>(null);
  const [analyzing, setAnalyzing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const startedRef = useRef(false); // guard so auto-analyze runs only once

  // Cancel while still waiting (upload/OCR/analysis) — processing is already running
  // server-side, so "cancel" means delete the document outright, not just stop watching it;
  // otherwise it would silently finish and show up in the dashboard later anyway. Same
  // silent-failure convention as the dashboard's own delete button: leave the page in place
  // so the user can just try again.
  const cancel = async () => {
    setCancelling(true);
    try {
      await api.deleteDocument(id);
      router.push("/dashboard");
    } catch {
      setCancelling(false);
    }
  };

  const analyze = async () => {
    setAnalyzing(true);
    setError(null);
    try {
      const result = await api.analyze(id);
      setDoc((d) => (d ? { ...d, analysis: result } : d));
    } catch (err) {
      setError((err as Error).message || t("doc.analyzeError"));
    } finally {
      setAnalyzing(false);
    }
  };

  // Load the document, and while OCR is still running (Status Pending/Processing) poll for
  // progress. Once it reaches Ready, automatically analyze it (once).
  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const tick = () => {
      api
        .getDocument(id)
        .then((d) => {
          if (cancelled) return;
          setDoc(d);
          if (d.status === DOCUMENT_STATUS.Pending || d.status === DOCUMENT_STATUS.Processing) {
            timer = setTimeout(tick, POLL_INTERVAL_MS);
            return;
          }
          if (d.status === DOCUMENT_STATUS.Ready && !d.analysis && !startedRef.current) {
            startedRef.current = true;
            analyze();
          }
        })
        .catch(() => {});
    };
    tick();

    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (!doc) return <p className="text-gray-500 dark:text-gray-400">{t("common.loading")}</p>;

  const ocrInProgress = doc.status === DOCUMENT_STATUS.Pending || doc.status === DOCUMENT_STATUS.Processing;
  const ocrFailed = doc.status === DOCUMENT_STATUS.Failed;
  const Back = rtl ? ArrowRight : ArrowLeft;

  return (
    <div className="space-y-6">
      <Link
        href="/dashboard"
        className="inline-flex items-center gap-2 text-sm font-medium text-gray-500 hover:text-brand dark:text-gray-400"
      >
        <Back className="h-4 w-4" />
        {t("doc.backToDashboard")}
      </Link>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{doc.fileName}</h1>
        {doc.analysis ? (
          <div className="flex flex-wrap gap-3">
            <Link href={`/documents/${id}/chat`}>
              <Button variant="outline"><MessageCircle className="h-5 w-5" />{t("doc.chat")}</Button>
            </Link>
            <ShareButton />
          </div>
        ) : (
          // Still waiting (upload/OCR/analysis) or it failed with nothing to show yet —
          // give the user a way out instead of being stuck watching a spinner.
          <Button variant="outline" onClick={() => setConfirmingCancel(true)} disabled={cancelling}>
            <X className="h-5 w-5" />{t("doc.cancel")}
          </Button>
        )}
      </div>

      {/* Pages are saved at upload time, before OCR runs — show them as soon as they exist so
          the user can verify what was photographed, even if OCR later fails on all of them. */}
      {doc.status !== DOCUMENT_STATUS.Pending && doc.totalPages > 0 && (
        <DocumentPages documentId={id} totalPages={doc.totalPages} />
      )}

      {doc.status === DOCUMENT_STATUS.Ready && doc.skippedPages > 0 && (
        <p role="status" className="rounded-xl bg-amber-50 px-4 py-3 text-amber-800 dark:bg-amber-950/40 dark:text-amber-200">
          {t("doc.pagesSkipped").replace("{n}", String(doc.skippedPages))}
        </p>
      )}

      {/* Pages are still being OCR'd in the background — real progress, not a guess. */}
      {ocrInProgress && <AnalyzingState mode="ocr" processedPages={doc.processedPages} totalPages={doc.totalPages} />}

      {ocrFailed && (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            <p role="alert" className="text-red-600">{doc.processingError || t("doc.ocrFailed")}</p>
          </CardContent>
        </Card>
      )}

      {/* While analyzing, show a friendly multi-step waiting state. */}
      {!ocrInProgress && !ocrFailed && !doc.analysis && analyzing && <AnalyzingState mode="analyze" />}

      {/* If analysis failed, let the user retry with one tap. */}
      {!ocrInProgress && !ocrFailed && !doc.analysis && !analyzing && (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            {error && <p role="alert" className="text-red-600">{error}</p>}
            <Button onClick={analyze} size="lg">
              <Sparkles className="h-5 w-5" />{t("doc.analyze")}
            </Button>
          </CardContent>
        </Card>
      )}

      {doc.analysis && <AnalysisCard analysis={doc.analysis} documentId={id} />}

      <ConfirmDialog
        open={confirmingCancel}
        title={t("doc.delete")}
        body={t("doc.confirmDelete")}
        confirmLabel={t("doc.delete")}
        destructive
        onConfirm={() => { setConfirmingCancel(false); cancel(); }}
        onCancel={() => setConfirmingCancel(false)}
      />
    </div>
  );
}
