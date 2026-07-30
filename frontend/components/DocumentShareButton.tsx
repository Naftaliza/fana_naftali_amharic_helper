"use client";

import { Printer, Send } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { buildPlainTextSummary } from "@/lib/documentText";
import type { AnalysisResult } from "@/lib/types";
import { Button } from "@/components/ui/button";

/**
 * "Send / print this explanation" — unlike ShareButton (which shares the app's own URL to grow
 * signups), this shares the document's actual plain-language content: the thing a user needs to
 * hand to a relative or bring to a counter. No backend involved; the text is built client-side
 * from the already-loaded analysis.
 */
export function DocumentShareButton({ analysis }: { analysis: AnalysisResult }) {
  const { t, language } = useLanguage();

  const send = async () => {
    const summary = buildPlainTextSummary(analysis, language, t);
    const text = t("doc.sendMessage").replace("{summary}", summary);
    if (navigator.share) {
      try {
        await navigator.share({ text });
        return;
      } catch {
        return; // user dismissed the sheet
      }
    }
    window.open(`https://wa.me/?text=${encodeURIComponent(text)}`, "_blank", "noopener");
  };

  return (
    <div className="flex gap-3" data-print-hide>
      <Button variant="outline" onClick={send}>
        <Send className="h-5 w-5" />{t("doc.send")}
      </Button>
      <Button variant="outline" onClick={() => window.print()}>
        <Printer className="h-5 w-5" />{t("doc.print")}
      </Button>
    </div>
  );
}
