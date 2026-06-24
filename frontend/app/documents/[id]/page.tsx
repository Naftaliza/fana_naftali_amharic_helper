"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { MessageCircle, Sparkles } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import type { DocumentDetail } from "@/lib/types";
import { AnalysisCard } from "@/components/AnalysisCard";
import { AnalyzingState } from "@/components/AnalyzingState";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

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

  // Load the document, then automatically analyze it if it hasn't been analyzed yet.
  useEffect(() => {
    api.getDocument(id).then((d) => {
      setDoc(d);
      if (d && !d.analysis && d.ocrText && !startedRef.current) {
        startedRef.current = true;
        analyze();
      }
    }).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (!doc) return <p className="text-gray-500">{t("common.loading")}</p>;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{doc.fileName}</h1>
        {doc.analysis && (
          <Link href={`/documents/${id}/chat`}>
            <Button variant="outline"><MessageCircle className="h-5 w-5" />{t("doc.chat")}</Button>
          </Link>
        )}
      </div>

      {/* While analyzing, show a friendly multi-step waiting state. */}
      {!doc.analysis && analyzing && <AnalyzingState />}

      {/* If analysis failed, let the user retry with one tap. */}
      {!doc.analysis && !analyzing && (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            {error && <p className="text-red-600">{error}</p>}
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
