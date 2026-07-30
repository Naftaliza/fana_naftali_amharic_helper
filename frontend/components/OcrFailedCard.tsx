"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Camera, ChevronDown, ImageUp, RefreshCw } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { mapOcrError } from "@/lib/ocrErrors";
import { CameraCapture } from "@/components/CameraCapture";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

const ACCEPT = "image/*,application/pdf,.pdf,.jpg,.jpeg,.png";

/**
 * Shown when a document's background OCR pipeline failed. Existing pages are immutable once
 * uploaded, so "retake"/"choose a different file" can't repair the failed document in place —
 * instead they upload a fresh document and delete the failed one, reusing the same two API calls
 * every other upload/delete flow already uses.
 */
export function OcrFailedCard({
  documentId,
  rawError,
  onRetryOcr,
  retrying,
}: {
  documentId: string;
  rawError: string | null;
  onRetryOcr: () => void;
  retrying: boolean;
}) {
  const { t } = useLanguage();
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [cameraOpen, setCameraOpen] = useState(false);
  const [replacing, setReplacing] = useState(false);
  const [replaceError, setReplaceError] = useState<string | null>(null);
  const [showDetails, setShowDetails] = useState(false);

  const replaceWith = async (files: File[]) => {
    if (!files.length) return;
    setReplacing(true);
    setReplaceError(null);
    try {
      const uploaded = await api.uploadDocument(files);
      await api.deleteDocument(documentId);
      router.push(`/documents/${uploaded.id}`);
    } catch (err) {
      setReplaceError((err as Error).message);
      setReplacing(false);
    }
  };

  const busy = retrying || replacing;

  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-4 py-12 text-center">
        <p role="alert" className="text-red-600">{mapOcrError(rawError, t)}</p>

        {rawError && (
          <div className="text-sm">
            <button
              onClick={() => setShowDetails((s) => !s)}
              className="inline-flex items-center gap-1 text-gray-500 underline dark:text-gray-400"
            >
              <ChevronDown className="h-4 w-4" />{t("doc.ocrDetails")}
            </button>
            {showDetails && <p className="mt-1 max-w-md break-words text-gray-400 dark:text-gray-500">{rawError}</p>}
          </div>
        )}

        <div className="flex flex-wrap justify-center gap-3">
          <Button onClick={onRetryOcr} disabled={busy}>
            <RefreshCw className="h-5 w-5" />{t("doc.retryOcr")}
          </Button>
          <Button variant="outline" onClick={() => setCameraOpen(true)} disabled={busy}>
            <Camera className="h-5 w-5" />{t("doc.retakePhoto")}
          </Button>
          <Button variant="outline" onClick={() => inputRef.current?.click()} disabled={busy}>
            <ImageUp className="h-5 w-5" />{t("upload.orChooseFile")}
          </Button>
        </div>

        {replaceError && <p role="alert" className="text-sm text-red-600">{replaceError}</p>}

        <input
          ref={inputRef}
          type="file"
          accept={ACCEPT}
          multiple
          className="hidden"
          onChange={(e) => replaceWith(Array.from(e.target.files ?? []))}
        />
        {cameraOpen && (
          <CameraCapture
            onClose={() => setCameraOpen(false)}
            onChooseFile={() => inputRef.current?.click()}
            onCapture={(files) => { setCameraOpen(false); replaceWith(files); }}
          />
        )}
      </CardContent>
    </Card>
  );
}
