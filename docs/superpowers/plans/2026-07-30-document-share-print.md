# Document Share/Print Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a user something to actually take away from a finished analysis — send the plain-language explanation to a relative, or print it to bring to a counter — instead of the current `ShareButton`, which only shares the app's own URL and reads as "share this app" when it sits next to a finished analysis.

**Architecture:** A pure text-builder function turns an `AnalysisResult` into a plain-text summary (type, urgency, summary, actions, deadlines) in the current language. A new `DocumentShareButton` component uses it for `navigator.share`/WhatsApp-fallback ("Send") and exposes a "Print" button that calls `window.print()`. A `@media print` block in `globals.css`, driven by a `data-print-hide` attribute (not fragile Tailwind class selectors, several of which — e.g. `fixed` — are reused across unrelated overlay components), hides chrome that shouldn't appear on a printed page. The existing growth-loop `ShareButton` stays only on the trial-result panel and gets a relabel so it no longer competes with the new per-document action.

**Tech Stack:** Next.js/React, Jest + @testing-library/react, CSS `@media print`.

---

## File Structure

- Create: `frontend/lib/documentText.ts` — `buildPlainTextSummary(analysis, language, t)`.
- Create: `frontend/lib/__tests__/documentText.test.ts`
- Create: `frontend/components/DocumentShareButton.tsx` — the "Send" + "Print" buttons.
- Create: `frontend/components/__tests__/DocumentShareButton.test.tsx`
- Modify: `frontend/app/documents/[id]/page.tsx` — swap `<ShareButton />` (line 116) for `<DocumentShareButton doc={doc} />`, add `data-print-hide` to the cancel/nav bits that shouldn't print.
- Modify: `frontend/components/Navbar.tsx` — add `data-print-hide` to the root `<nav>`.
- Modify: `frontend/components/AccessibilityWidget.tsx` — add `data-print-hide` to its root `<div>`.
- Modify: `frontend/components/SkipLink.tsx` — add `data-print-hide`.
- Modify: `frontend/components/AnalysisCard.tsx` — add `data-print-hide` to the sticky audio-player wrapper (lines 126-164).
- Modify: `frontend/components/ReferralBlock.tsx` — add `data-print-hide` to its outer wrapper (referral offers don't belong on a printed hand-off document).
- Modify: `frontend/app/globals.css` — add the `@media print` block.
- Modify: `frontend/i18n/dictionaries.ts` — add `doc.send`, `doc.sendMessage`, `doc.print`; update `share.button`/`share.message` copy to disambiguate from the new action.

---

### Task 1: Plain-text summary builder

**Files:**
- Create: `frontend/lib/documentText.ts`
- Test: `frontend/lib/__tests__/documentText.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// frontend/lib/__tests__/documentText.test.ts
import { buildPlainTextSummary } from "@/lib/documentText";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });

const ANALYSIS: AnalysisResult = {
  summary: LOC("This is a tax notice."),
  documentType: LOC("Tax notice"),
  urgencyLevel: "High",
  keyPoints: [LOC("Payment is overdue"), LOC("A penalty may apply")],
  requiredActions: [
    { description: LOC("Pay the balance"), isMandatory: true },
    { description: LOC("Consider requesting an extension"), isMandatory: false },
  ],
  deadlines: [{ date: "2026-08-12T00:00:00Z", description: LOC("Payment due") }],
  explanation: LOC("The tax authority says you owe money from last year."),
};

const t = (key: string) => ({ "doc.required": "Required", "doc.urgency": "Urgency", "doc.type": "Type" } as Record<string, string>)[key] ?? key;

describe("buildPlainTextSummary", () => {
  it("includes the document type and urgency", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("Tax notice");
    expect(text).toContain("High");
  });

  it("includes the summary and explanation", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("This is a tax notice.");
    expect(text).toContain("The tax authority says you owe money from last year.");
  });

  it("includes every required action, marking mandatory ones", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("Pay the balance");
    expect(text).toContain("Consider requesting an extension");
    expect(text.indexOf("Required")).toBeGreaterThanOrEqual(0);
  });

  it("includes deadlines with a formatted date", () => {
    const text = buildPlainTextSummary(ANALYSIS, "en", t);
    expect(text).toContain("Payment due");
    expect(text).toMatch(/2026|Aug/);
  });

  it("omits the deadlines section entirely when there are none", () => {
    const text = buildPlainTextSummary({ ...ANALYSIS, deadlines: [] }, "en", t);
    expect(text).not.toMatch(/deadline/i);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest lib/__tests__/documentText.test.ts`
Expected: FAIL — `Cannot find module '@/lib/documentText'`

- [ ] **Step 3: Implement**

```ts
// frontend/lib/documentText.ts
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest lib/__tests__/documentText.test.ts`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/lib/documentText.ts frontend/lib/__tests__/documentText.test.ts
git commit -m "feat: add plain-text analysis summary builder for sharing/printing"
```

---

### Task 2: `doc.send` / `doc.print` i18n keys, and relabel `share.*`

**Files:**
- Modify: `frontend/i18n/dictionaries.ts`

- [ ] **Step 1: Add new keys to the Hebrew block** (near `doc.deadlines` etc., lines 6-319)

```ts
  "doc.send": "שליחה",
  "doc.sendMessage": "הסבר על המסמך שלי מהאפליקציה Fana:\n\n{summary}",
  "doc.print": "הדפסה",
```

- [ ] **Step 2: Add to the Amharic block** (lines 321-634)

```ts
  "doc.send": "ላክ",
  "doc.sendMessage": "ስለ ሰነዴ ማብራሪያ ከ Fana መተግበሪያ፡\n\n{summary}",
  "doc.print": "አትም",
```

- [ ] **Step 3: Add to the English block** (lines 636-949)

```ts
  "doc.send": "Send",
  "doc.sendMessage": "Explanation of my document from Fana:\n\n{summary}",
  "doc.print": "Print",
```

- [ ] **Step 4: Relabel `share.button` / `share.message` in all three blocks so it reads as a growth-loop action, not "share this document"**

Change existing values (Hebrew line 158-159, Amharic line 473-474, English line 788-789):

```ts
// he
  "share.button": "ספרו לחבר על Fana",
  "share.message": "מצאתי אפליקציה שמסבירה מסמכים רשמיים בעברית — באמהרית ובעברית פשוטה. כדאי לנסות: {url}",
// am
  "share.button": "ስለ Fana ለጓደኛ ንገሩ",
  "share.message": "ኦፊሴላዊ የዕብራይስጥ ሰነዶችን በአማርኛ የሚያብራራ መተግበሪያ አገኘሁ — ፋና። ይሞክሩት፡ {url}",
// en
  "share.button": "Tell a friend about Fana",
  "share.message": "I found an app that explains official Hebrew documents in Amharic — Fana. Give it a try: {url}",
```

(`share.message` text is unchanged — only `share.button`'s label changes, from the generic "Share Fana" to an explicit growth-loop verb, so it reads distinctly from the new "Send"/"Print" document actions.)

- [ ] **Step 5: Run the types/dictionary sanity test**

Run: `cd frontend && npx jest lib/__tests__/types.test.ts`
Expected: PASS (no test currently asserts on these specific strings, so this just confirms nothing else broke)

- [ ] **Step 6: Commit**

```bash
git add frontend/i18n/dictionaries.ts
git commit -m "feat: add doc.send/doc.print keys, relabel growth-loop ShareButton"
```

---

### Task 3: `DocumentShareButton` component

**Files:**
- Create: `frontend/components/DocumentShareButton.tsx`
- Test: `frontend/components/__tests__/DocumentShareButton.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/DocumentShareButton.test.tsx
import { render, screen, fireEvent } from "@testing-library/react";
import { DocumentShareButton } from "@/components/DocumentShareButton";
import { LanguageProvider } from "@/lib/language-context";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });
const ANALYSIS: AnalysisResult = {
  summary: LOC("Summary"), documentType: LOC("Letter"), urgencyLevel: "Low",
  keyPoints: [], requiredActions: [], deadlines: [], explanation: LOC("Explanation"),
};

function renderButton() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <DocumentShareButton analysis={ANALYSIS} />
    </LanguageProvider>
  );
}

describe("DocumentShareButton", () => {
  const originalShare = (navigator as any).share;
  const originalOpen = window.open;
  const originalPrint = window.print;

  afterEach(() => {
    (navigator as any).share = originalShare;
    window.open = originalOpen;
    window.print = originalPrint;
    jest.restoreAllMocks();
  });

  it("renders Send and Print buttons", () => {
    renderButton();
    expect(screen.getByText("Send")).toBeInTheDocument();
    expect(screen.getByText("Print")).toBeInTheDocument();
  });

  it("uses navigator.share with the plain-text summary when available", async () => {
    const share = jest.fn().mockResolvedValue(undefined);
    (navigator as any).share = share;
    renderButton();

    fireEvent.click(screen.getByText("Send"));
    await Promise.resolve();

    expect(share).toHaveBeenCalledTimes(1);
    const arg = share.mock.calls[0][0];
    expect(arg.text).toContain("Letter");
    expect(arg.text).toContain("Summary");
  });

  it("falls back to a WhatsApp deep link when navigator.share is unavailable", () => {
    (navigator as any).share = undefined;
    window.open = jest.fn();
    renderButton();

    fireEvent.click(screen.getByText("Send"));

    expect(window.open).toHaveBeenCalledWith(
      expect.stringContaining("https://wa.me/?text="),
      "_blank",
      "noopener"
    );
  });

  it("calls window.print when Print is clicked", () => {
    window.print = jest.fn();
    renderButton();

    fireEvent.click(screen.getByText("Print"));

    expect(window.print).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/DocumentShareButton.test.tsx`
Expected: FAIL — `Cannot find module '@/components/DocumentShareButton'`

- [ ] **Step 3: Implement**

```tsx
// frontend/components/DocumentShareButton.tsx
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/DocumentShareButton.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/components/DocumentShareButton.tsx frontend/components/__tests__/DocumentShareButton.test.tsx
git commit -m "feat: add DocumentShareButton (send plain-text explanation / print)"
```

---

### Task 4: Wire into the document page

**Files:**
- Modify: `frontend/app/documents/[id]/page.tsx`
- Test: extend `frontend/app/documents/[id]/__tests__/page.test.tsx` (created in the confirm-dialog plan's Task 4 — if that plan hasn't run yet, create this file fresh following the same mocking pattern shown there)

Current code (`documents/[id]/page.tsx:111-117`):

```tsx
        {doc.analysis ? (
          <div className="flex flex-wrap gap-3">
            <Link href={`/documents/${id}/chat`}>
              <Button variant="outline"><MessageCircle className="h-5 w-5" />{t("doc.chat")}</Button>
            </Link>
            <ShareButton />
          </div>
        ) : (
```

- [ ] **Step 1: Write the failing test (append to the existing test file)**

```tsx
  it("shows Send/Print document actions instead of the generic ShareButton once analyzed", async () => {
    (api.getDocument as jest.Mock).mockResolvedValue({
      ...PENDING_DOC,
      status: 2,
      analysis: {
        summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "t" },
        urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
        explanation: { he: "e", am: "e", en: "e" },
      },
    });
    renderPage();
    await waitFor(() => screen.getByText("Send"));
    expect(screen.getByText("Print")).toBeInTheDocument();
    expect(screen.queryByText("Tell a friend about Fana")).not.toBeInTheDocument();
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest "app/documents/[id]/__tests__/page.test.tsx"`
Expected: FAIL — the old `<ShareButton />` renders "Tell a friend about Fana" (after Task 2's relabel) instead of "Send"/"Print".

- [ ] **Step 3: Implement**

Replace the import (remove `ShareButton`, add `DocumentShareButton`):

```tsx
import { DocumentShareButton } from "@/components/DocumentShareButton";
```

Replace the JSX:

```tsx
        {doc.analysis ? (
          <div className="flex flex-wrap gap-3">
            <Link href={`/documents/${id}/chat`}>
              <Button variant="outline"><MessageCircle className="h-5 w-5" />{t("doc.chat")}</Button>
            </Link>
            <DocumentShareButton analysis={doc.analysis} />
          </div>
        ) : (
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest "app/documents/[id]/__tests__/page.test.tsx"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add "frontend/app/documents/[id]/page.tsx" "frontend/app/documents/[id]/__tests__/page.test.tsx"
git commit -m "feat: show Send/Print document actions on the document page instead of the app-share button"
```

---

### Task 5: Print stylesheet

**Files:**
- Modify: `frontend/app/globals.css`
- Modify: `frontend/components/Navbar.tsx`
- Modify: `frontend/components/AccessibilityWidget.tsx`
- Modify: `frontend/components/SkipLink.tsx`
- Modify: `frontend/components/AnalysisCard.tsx`
- Modify: `frontend/components/ReferralBlock.tsx`

This is a visual/print concern jsdom cannot assert on meaningfully (`@media print` doesn't apply during a jsdom render, and there's no headless-print-preview tooling in this repo). Verification is manual: build the app, open a finished document page, and use the browser's print preview.

- [ ] **Step 1: Tag chrome components with `data-print-hide`**

`Navbar.tsx` — find the root element (`Navbar.tsx:51`, the `<nav>`) and add the attribute:
```tsx
      <nav aria-label={t("app.name")} data-print-hide className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4">
```

`AccessibilityWidget.tsx:75` — its root `<div>`:
```tsx
    <div ref={ref} data-print-hide className="pb-safe fixed bottom-4 end-4 z-50 flex flex-col items-end gap-3">
```

`SkipLink.tsx` — read the file first (`cat frontend/components/SkipLink.tsx`) and add `data-print-hide` to its root anchor/element the same way.

`AnalysisCard.tsx:126` — the sticky audio-player wrapper:
```tsx
      <div data-print-hide className="sticky top-20 z-30 flex flex-col gap-1">
```

`ReferralBlock.tsx` — read its return statement's outer wrapper (past line 40, not yet read in full) and add `data-print-hide` to whatever it renders at the top level; if it returns `null` for no-match cases that's already fine, this only matters for the rendered-content path.

- [ ] **Step 2: Add the print rule to `globals.css`**

Append at the end of the file:

```css
/* Printing an analysis: strip everything that isn't the document's own explanation — nav,
   accessibility widget, audio player, and sponsored referrals don't belong on a sheet someone
   hands to a clerk or a relative. Driven by an explicit data attribute rather than Tailwind
   utility-class selectors (several, like `fixed`, are reused by unrelated overlay components
   and would be fragile to key off directly). */
@media print {
  [data-print-hide] {
    display: none !important;
  }
  body {
    background: none !important;
  }
  main {
    max-width: 100% !important;
    padding: 0 !important;
  }
}
```

- [ ] **Step 3: Manual verification**

Run: `cd frontend && npm run dev`, sign in, open a document with a completed analysis, open the browser's print preview (Ctrl/Cmd+P). Confirm: navbar, accessibility widget, audio player, and referral block are absent; the analysis cards (summary, explanation, key points, actions, deadlines) flow cleanly on the page.

- [ ] **Step 4: Commit**

```bash
git add frontend/app/globals.css frontend/components/Navbar.tsx frontend/components/AccessibilityWidget.tsx frontend/components/SkipLink.tsx frontend/components/AnalysisCard.tsx frontend/components/ReferralBlock.tsx
git commit -m "feat: add print stylesheet hiding app chrome, keep only the analysis content"
```

---

## Self-Review Notes

- **Spec coverage:** plain-text builder (type/urgency/summary/actions/deadlines) ✓ Task 1; navigator.share + WhatsApp fallback, same pattern as ShareButton/ReferralBlock ✓ Task 3; `@media print` block hiding navbar/accessibility widget/audio player/referral block ✓ Task 5; `window.print()` ✓ Task 3; relabel existing ShareButton so the two actions stop competing ✓ Task 2 + Task 4 (ShareButton now only renders on the trial-result panel in `UploadExperience.tsx`, untouched by this plan, with its new "Tell a friend..." label).
- **Scope boundary:** this plan does not touch the account-wide GDPR export (`GET /api/users/me/export`) — that remains a separate, intentionally different feature (whole-account JSON dump vs. this plan's single-document plain-text share/print).
