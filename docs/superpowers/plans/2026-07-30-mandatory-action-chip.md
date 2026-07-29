# Mandatory Action "Required" Chip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A mandatory required action must be distinguishable by more than color/shape alone — add a translated text chip ("Required" / "חובה" / "ግዴታ") next to the `AlertTriangle` icon in `AnalysisCard`, so the distinction survives grayscale mode and screen readers.

**Architecture:** Single small JSX change in `AnalysisCard.tsx`'s actions list: mark the icon `aria-hidden`, add a `<span>` pill reusing the existing urgency-badge pill styling, driven by a new `doc.required` dictionary key.

**Tech Stack:** Next.js/React, Jest + @testing-library/react, existing `useLanguage()`.

---

## Pre-existing state check (read before starting)

The ticket claims "the audio never mentions it either." That's **already false** — `backend/src/AmharicHelper.Application/Tts/SpokenTextBuilder.cs:16-23,75-83` already defines a `Required` label per language (`"חובה"` / `"ግዴታ"` / `"Required"`) and prepends it to any mandatory action's spoken text (`return a.IsMandatory ? $"{l.Required}: {desc}" : desc;`). **Do not duplicate this work.** This plan is frontend-visual only. If you find the audio doesn't actually say it when testing, that's a regression to investigate separately, not a gap this plan needs to fill.

## File Structure

- Modify: `frontend/components/AnalysisCard.tsx` (lines 213-223, the actions `<CardContent>`)
- Modify: `frontend/i18n/dictionaries.ts` — add `doc.required` to all three blocks
- Test: `frontend/components/__tests__/AnalysisCard.test.tsx` (new — check first whether one already exists with `ls frontend/components/__tests__/` before creating)

---

### Task 1: Add the `doc.required` translation key

**Files:**
- Modify: `frontend/i18n/dictionaries.ts`

- [ ] **Step 1: Add to the Hebrew block**

Add near `"doc.deadlines"` or any other `doc.*` key in the `he` block (lines 6-319) — exact position doesn't matter, dictionaries are flat maps:

```ts
  "doc.required": "חובה",
```

- [ ] **Step 2: Add to the Amharic block** (lines 321-634)

```ts
  "doc.required": "ግዴታ",
```

- [ ] **Step 3: Add to the English block** (lines 636-949)

```ts
  "doc.required": "Required",
```

These three strings match `SpokenTextBuilder.cs`'s existing `Labels.Required` exactly (`"חובה"`, `"ግዴታ"`, `"Required"`) — the visible chip and the spoken word should say the same thing.

- [ ] **Step 4: Commit**

```bash
git add frontend/i18n/dictionaries.ts
git commit -m "feat: add doc.required translation key for the mandatory-action chip"
```

---

### Task 2: Render the chip in `AnalysisCard`

**Files:**
- Modify: `frontend/components/AnalysisCard.tsx`
- Test: `frontend/components/__tests__/AnalysisCard.test.tsx`

Current code (`AnalysisCard.tsx:213-223`):

```tsx
          <CardContent>
            <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.requiredActions.map((a, i) => (
                <li key={i} className="flex items-start gap-2">
                  {a.isMandatory && <AlertTriangle className="mt-1 h-4 w-4 shrink-0 text-orange-500" />}
                  <span>{loc(a.description, language)}</span>
                </li>
              ))}
            </ul>
          </CardContent>
```

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/AnalysisCard.test.tsx
import { render, screen } from "@testing-library/react";
import { AnalysisCard } from "@/components/AnalysisCard";
import { LanguageProvider } from "@/lib/language-context";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });

const ANALYSIS: AnalysisResult = {
  summary: LOC("Summary text"),
  documentType: LOC("Letter"),
  urgencyLevel: "Medium",
  keyPoints: [LOC("Point one")],
  requiredActions: [
    { description: LOC("Pay the fee"), isMandatory: true },
    { description: LOC("Consider appealing"), isMandatory: false },
  ],
  deadlines: [],
  explanation: LOC("Explanation text"),
};

function renderCard() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <AnalysisCard analysis={ANALYSIS} />
    </LanguageProvider>
  );
}

describe("AnalysisCard mandatory action chip", () => {
  it("shows a 'Required' text chip next to mandatory actions", () => {
    renderCard();
    expect(screen.getByText("Required")).toBeInTheDocument();
  });

  it("does not show the chip next to optional actions", () => {
    renderCard();
    const items = screen.getAllByRole("listitem");
    const optionalItem = items.find((li) => li.textContent?.includes("Consider appealing"))!;
    expect(optionalItem.textContent).not.toContain("Required");
  });

  it("hides the warning icon from assistive tech (the text chip carries the meaning now)", () => {
    const { container } = renderCard();
    const icon = container.querySelector("svg.text-orange-500");
    expect(icon).toHaveAttribute("aria-hidden", "true");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/AnalysisCard.test.tsx`
Expected: FAIL — no text "Required" anywhere in the rendered output (`getByText("Required")` throws), since the language default before Task 1 is a plain object key fallback and no chip exists yet regardless.

- [ ] **Step 3: Implement**

```tsx
          <CardContent>
            <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.requiredActions.map((a, i) => (
                <li key={i} className="flex items-start gap-2">
                  {a.isMandatory && (
                    <>
                      <AlertTriangle aria-hidden="true" className="mt-1 h-4 w-4 shrink-0 text-orange-500" />
                      <span className="shrink-0 rounded-full bg-orange-100 px-2 py-0.5 text-xs font-medium text-orange-800 dark:bg-orange-900/40 dark:text-orange-300">
                        {t("doc.required")}
                      </span>
                    </>
                  )}
                  <span>{loc(a.description, language)}</span>
                </li>
              ))}
            </ul>
          </CardContent>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/AnalysisCard.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/components/AnalysisCard.tsx frontend/components/__tests__/AnalysisCard.test.tsx
git commit -m "feat: add visible 'Required' chip to mandatory actions, not just color/icon"
```

---

## Self-Review Notes

- **Spec coverage:** text chip ✓, reuses existing pill styling pattern (urgency badge look) ✓, triangle marked `aria-hidden` ✓. "Speak the same word in SpokenTextBuilder" — already exists in the codebase (see the pre-existing-state note above); this plan does not touch backend TTS code since it would be redundant/no-op work.
- **Grayscale verification (manual, not automatable in jsdom):** after implementing, run the app, toggle the grayscale accessibility option, and confirm the orange pill's background/text still reads as a distinct block from the surrounding card even under `filter: grayscale(100%)` — grayscale preserves luminance contrast, and `bg-orange-100`/`text-orange-800` have enough lightness difference from the plain list text to remain visually distinct as a chip shape, independent of hue.
