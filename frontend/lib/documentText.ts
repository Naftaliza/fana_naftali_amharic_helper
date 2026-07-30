import { loc, URGENCY_ENUM, type AnalysisResult, type Language } from "@/lib/types";

const URGENCY_LABEL: Record<Language, string[]> = {
  en: ["Low", "Medium", "High", "Critical"],
  he: ["נמוכה", "בינונית", "גבוהה", "קריטית"],
  am: ["ዝቅተኛ", "መካከለኛ", "ከፍተኛ", "አስቸኳይ"],
};

/**
 * Plain-text version of an analysis for sharing/printing outside the app — a relative reading
 * over WhatsApp, or a sheet brought to a counter, has no use for the app's own UI chrome.
 */
export function buildPlainTextSummary(
  analysis: AnalysisResult,
  language: Language,
  t: (key: string) => string
): string {
  const urgencyIndex =
    typeof analysis.urgencyLevel === "number" ? analysis.urgencyLevel : URGENCY_ENUM[analysis.urgencyLevel] ?? 0;
  const lines: string[] = [];

  lines.push(loc(analysis.documentType, language));
  lines.push(`${t("doc.urgency")}: ${URGENCY_LABEL[language][urgencyIndex] ?? URGENCY_LABEL[language][0]}`);
  lines.push("");
  lines.push(loc(analysis.summary, language));
  lines.push("");
  lines.push(loc(analysis.explanation, language));

  if (analysis.requiredActions.length > 0) {
    lines.push("");
    for (const a of analysis.requiredActions) {
      const prefix = a.isMandatory ? `[${t("doc.required")}] ` : "";
      lines.push(`- ${prefix}${loc(a.description, language)}`);
    }
  }

  if (analysis.deadlines.length > 0) {
    lines.push("");
    for (const d of analysis.deadlines) {
      const date = d.date ? new Date(d.date).toLocaleDateString(language) : "";
      lines.push(`- ${date ? `${date}: ` : ""}${loc(d.description, language)}`);
    }
  }

  return lines.join("\n").trim();
}
