// Translates the raw backend processingError string (see DocumentProcessor.cs) into a plain-
// language cause a non-technical, possibly non-English-reading user can act on. The raw string
// itself is never shown as the primary message — it's always available behind a details toggle
// (see OcrFailedCard) for anyone who wants to report the exact error.

export function mapOcrError(raw: string | null, t: (key: string) => string): string {
  if (!raw) return t("doc.ocrFailed");
  if (raw.includes("No readable text")) return t("doc.ocrFailedUnreadable");
  if (raw.toLowerCase().includes("api key") || raw.toLowerCase().includes("temporarily unavailable"))
    return t("doc.ocrFailedService");
  return t("doc.ocrFailed");
}
