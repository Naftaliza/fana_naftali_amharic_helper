"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { MessageCircle, Sparkles } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { DOCUMENT_STATUS, type DocumentDetail } from "@/lib/types";
import { AnalysisCard } from "@/components/AnalysisCard";
import { AnalyzingState } from "@/components/AnalyzingState";
import { ShareButton } from "@/components/ShareButton";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

// How often to poll GET /documents/{id} while the background OCR pipeline (DocumentProcessor)
// is still working through the document's pages.
const POLL_INTERVAL_MS = 1500;

export default function DocumentDetailPage() {
  const { t } = useLanguage();
  const params = useParams();
  const id = params.id as string;
  const [doc, setDoc] = useState<DocumentDetail | null>(null);
  const [analyzing, setAnalyzing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const startedRef = useRef(false); // guard so auto-analyze runs only once

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

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{doc.fileName}</h1>
        {doc.analysis && (
          <div className="flex flex-wrap gap-3">
            <Link href={`/documents/${id}/chat`}>
              <Button variant="outline"><MessageCircle className="h-5 w-5" />{t("doc.chat")}</Button>
            </Link>
            <ShareButton />
          </div>
        )}
      </div>

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
    </div>
  );
}
