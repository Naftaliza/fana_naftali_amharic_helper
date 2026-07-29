# Deadline & Action Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make an analysis's required actions and deadlines survive closing the tab: a persisted checkbox per required action, an "Add to calendar" `.ics` download per dated deadline, and a "Coming up" strip on the dashboard showing the nearest upcoming deadlines across every document.

**Architecture:** Action-checked state lives in `localStorage`, keyed by document id, same flat-module pattern as `lib/onboarding.ts`/`lib/trial.ts`. Calendar events are generated entirely client-side as an `.ics` blob — no backend, no permissions, works with the phone's own calendar app. The "Coming up" strip needs every document's deadlines up front (the dashboard currently only fetches `DocumentSummary`, which has no deadline data), so `GET /api/documents` gets a small DTO addition reusing the existing `DeadlineDto` shape already used by `DocumentDetailDto`.

**Tech Stack:** Next.js/React, Jest + @testing-library/react on the frontend; ASP.NET Core/MediatR + xUnit on the backend (no mocking library — hand-written fakes per existing test convention).

---

## File Structure

- Modify: `backend/src/AmharicHelper.Application/DTOs/DocumentDtos.cs` — add `Deadlines` to `DocumentSummaryDto`.
- Modify: `backend/src/AmharicHelper.Application/Features/Documents/DocumentQueries.cs` — populate it in `ListDocumentsHandler`.
- Create: `backend/tests/AmharicHelper.UnitTests/ListDocumentsHandlerTests.cs`
- Modify: `frontend/lib/types.ts` — add `deadlines` to `DocumentSummary`.
- Create: `frontend/lib/actionProgress.ts` — per-document checked-action-index persistence.
- Create: `frontend/lib/__tests__/actionProgress.test.ts`
- Create: `frontend/lib/ics.ts` — `.ics` blob builder.
- Create: `frontend/lib/__tests__/ics.test.ts`
- Modify: `frontend/components/AnalysisCard.tsx` — checkboxes on required actions, "Add to calendar" per deadline.
- Modify: `frontend/components/__tests__/AnalysisCard.test.tsx` (created by the mandatory-action-chip plan — extend it; if that plan hasn't run yet, create the file following the same pattern shown there)
- Create: `frontend/components/ComingUpStrip.tsx`
- Create: `frontend/components/__tests__/ComingUpStrip.test.tsx`
- Modify: `frontend/app/dashboard/page.tsx` — render `ComingUpStrip` above the document list.

---

### Task 1: Backend — include deadlines in the document list DTO

**Files:**
- Modify: `backend/src/AmharicHelper.Application/DTOs/DocumentDtos.cs`
- Modify: `backend/src/AmharicHelper.Application/Features/Documents/DocumentQueries.cs`
- Test: `backend/tests/AmharicHelper.UnitTests/ListDocumentsHandlerTests.cs`

Current DTO (`DocumentDtos.cs:5-11`):

```csharp
public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    bool HasAnalysis,
    DocumentProcessingStatus Status);
```

Current handler (`DocumentQueries.cs:10-26`):

```csharp
public class ListDocumentsHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses)
    : IRequestHandler<ListDocumentsQuery, Result<IReadOnlyList<DocumentSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentSummaryDto>>> Handle(ListDocumentsQuery q, CancellationToken ct)
    {
        var docs = await documents.ListByUserAsync(q.UserId, ct);
        var list = new List<DocumentSummaryDto>();
        foreach (var d in docs)
        {
            var hasAnalysis = await analyses.GetByDocumentIdAsync(d.Id, ct) is not null;
            list.Add(new DocumentSummaryDto(d.Id, d.FileName, d.ContentType, d.UploadedAt, hasAnalysis, d.Status));
        }
        return Result<IReadOnlyList<DocumentSummaryDto>>.Ok(list);
    }
}
```

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/AmharicHelper.UnitTests/ListDocumentsHandlerTests.cs
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class ListDocumentsHandlerTests
{
    private sealed class FakeDocs : IDocumentRepository
    {
        public List<Document> Docs { get; } = new();
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Docs.FirstOrDefault(d => d.Id == id));
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Docs.Where(d => d.UserId == userId).ToList());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) { Docs.Add(document); return Task.CompletedTask; }
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAnalyses : IDocumentAnalysisRepository
    {
        public Dictionary<Guid, DocumentAnalysis> ByDocumentId { get; } = new();
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(ByDocumentId.TryGetValue(documentId, out var a) ? a : null);
        public Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default) { ByDocumentId[analysis.DocumentId] = analysis; return Task.CompletedTask; }
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) { ByDocumentId.Remove(documentId); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Includes_deadlines_for_documents_with_analysis()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docs = new FakeDocs();
        docs.Docs.Add(new Document { Id = docId, UserId = userId, FileName = "letter.pdf" });

        var analyses = new FakeAnalyses();
        var deadlineDate = new DateTime(2026, 8, 12);
        analyses.ByDocumentId[docId] = new DocumentAnalysis
        {
            DocumentId = docId,
            Deadlines = new List<Deadline> { new(deadlineDate, new LocalizedText("שלמו", "ይክፈሉ", "Pay")) }
        };

        var handler = new ListDocumentsHandler(docs, analyses);
        var result = await handler.Handle(new ListDocumentsQuery(userId), default);

        Assert.True(result.Success);
        var summary = Assert.Single(result.Value!);
        var deadline = Assert.Single(summary.Deadlines);
        Assert.Equal(deadlineDate, deadline.Date);
        Assert.Equal("Pay", deadline.Description.En);
    }

    [Fact]
    public async Task Empty_deadlines_for_documents_with_no_analysis_yet()
    {
        var userId = Guid.NewGuid();
        var docs = new FakeDocs();
        docs.Docs.Add(new Document { Id = Guid.NewGuid(), UserId = userId, FileName = "letter.pdf" });

        var handler = new ListDocumentsHandler(docs, new FakeAnalyses());
        var result = await handler.Handle(new ListDocumentsQuery(userId), default);

        Assert.Empty(Assert.Single(result.Value!).Deadlines);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test --filter ListDocumentsHandlerTests`
Expected: FAIL — `DocumentSummaryDto` has no `Deadlines` member.

- [ ] **Step 3: Implement**

`DocumentDtos.cs:5-11`:

```csharp
public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    bool HasAnalysis,
    DocumentProcessingStatus Status,
    IReadOnlyList<DeadlineDto> Deadlines);
```

`DocumentQueries.cs:10-26`:

```csharp
public class ListDocumentsHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses)
    : IRequestHandler<ListDocumentsQuery, Result<IReadOnlyList<DocumentSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentSummaryDto>>> Handle(ListDocumentsQuery q, CancellationToken ct)
    {
        var docs = await documents.ListByUserAsync(q.UserId, ct);
        var list = new List<DocumentSummaryDto>();
        foreach (var d in docs)
        {
            var analysis = await analyses.GetByDocumentIdAsync(d.Id, ct);
            var deadlines = analysis?.Deadlines
                .Select(dl => new DeadlineDto { Date = dl.Date, Description = dl.Description })
                .ToList() ?? new List<DeadlineDto>();
            list.Add(new DocumentSummaryDto(d.Id, d.FileName, d.ContentType, d.UploadedAt, analysis is not null, d.Status, deadlines));
        }
        return Result<IReadOnlyList<DocumentSummaryDto>>.Ok(list);
    }
}
```

Note this also fixes a latent inefficiency: it now only calls `GetByDocumentIdAsync` once per document instead of computing `hasAnalysis` and then needing a second lookup for deadlines.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd backend && dotnet test --filter ListDocumentsHandlerTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Run the full backend suite to catch any other `DocumentSummaryDto` constructor usages**

Run: `cd backend && dotnet test`
Expected: PASS. If any other call site constructs `DocumentSummaryDto` positionally (grep for `new DocumentSummaryDto(` across `backend/src` first), update it to pass the new `Deadlines` argument.

- [ ] **Step 6: Commit**

```bash
git add backend/src/AmharicHelper.Application/DTOs/DocumentDtos.cs backend/src/AmharicHelper.Application/Features/Documents/DocumentQueries.cs backend/tests/AmharicHelper.UnitTests/ListDocumentsHandlerTests.cs
git commit -m "feat: include deadlines in the document list DTO"
```

---

### Task 2: Frontend type + `lib/actionProgress.ts`

**Files:**
- Modify: `frontend/lib/types.ts`
- Create: `frontend/lib/actionProgress.ts`
- Test: `frontend/lib/__tests__/actionProgress.test.ts`

- [ ] **Step 1: Add `deadlines` to `DocumentSummary`**

`types.ts:97-104` currently:

```ts
export interface DocumentSummary {
  id: string;
  fileName: string;
  contentType: string;
  uploadedAt: string;
  hasAnalysis: boolean;
  status: number;
}
```

Change to:

```ts
export interface DocumentSummary {
  id: string;
  fileName: string;
  contentType: string;
  uploadedAt: string;
  hasAnalysis: boolean;
  status: number;
  deadlines: { date: string | null; description: LocalizedText }[];
}
```

- [ ] **Step 2: Write the failing test for `actionProgress.ts`**

```ts
// frontend/lib/__tests__/actionProgress.test.ts
import { getCheckedActions, setActionChecked, isActionChecked } from "@/lib/actionProgress";

describe("actionProgress", () => {
  beforeEach(() => window.localStorage.clear());

  it("returns no checked actions for a document that's never been touched", () => {
    expect(getCheckedActions("doc-1")).toEqual([]);
    expect(isActionChecked("doc-1", 0)).toBe(false);
  });

  it("persists a checked action index for one document", () => {
    setActionChecked("doc-1", 2, true);
    expect(isActionChecked("doc-1", 2)).toBe(true);
    expect(getCheckedActions("doc-1")).toEqual([2]);
  });

  it("unchecking removes the index", () => {
    setActionChecked("doc-1", 2, true);
    setActionChecked("doc-1", 2, false);
    expect(isActionChecked("doc-1", 2)).toBe(false);
    expect(getCheckedActions("doc-1")).toEqual([]);
  });

  it("keeps different documents' progress independent", () => {
    setActionChecked("doc-1", 0, true);
    setActionChecked("doc-2", 0, true);
    setActionChecked("doc-1", 0, false);
    expect(isActionChecked("doc-1", 0)).toBe(false);
    expect(isActionChecked("doc-2", 0)).toBe(true);
  });

  it("tolerates corrupted storage instead of throwing", () => {
    window.localStorage.setItem("actionProgress:doc-1", "{not json");
    expect(getCheckedActions("doc-1")).toEqual([]);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd frontend && npx jest lib/__tests__/actionProgress.test.ts`
Expected: FAIL — `Cannot find module '@/lib/actionProgress'`

- [ ] **Step 4: Implement**

```ts
// frontend/lib/actionProgress.ts
// Persists which required actions the user has checked off, per document — same localStorage-flag
// approach as lib/onboarding.ts/lib/trial.ts, keyed per document like lib/leadFeedback.ts's blob.
// Actions have no stable id from the backend, so they're tracked by their position in
// analysis.requiredActions; re-running analysis on the same document resets progress since the
// action list itself may have changed.

const keyFor = (documentId: string) => `actionProgress:${documentId}`;

export function getCheckedActions(documentId: string): number[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(keyFor(documentId));
    const parsed = JSON.parse(raw ?? "[]");
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function isActionChecked(documentId: string, index: number): boolean {
  return getCheckedActions(documentId).includes(index);
}

export function setActionChecked(documentId: string, index: number, checked: boolean): void {
  const current = new Set(getCheckedActions(documentId));
  if (checked) current.add(index);
  else current.delete(index);
  window.localStorage.setItem(keyFor(documentId), JSON.stringify([...current].sort((a, b) => a - b)));
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx jest lib/__tests__/actionProgress.test.ts`
Expected: PASS (5 tests)

- [ ] **Step 6: Commit**

```bash
git add frontend/lib/types.ts frontend/lib/actionProgress.ts frontend/lib/__tests__/actionProgress.test.ts
git commit -m "feat: add per-document required-action checkbox persistence"
```

---

### Task 3: `.ics` calendar blob builder

**Files:**
- Create: `frontend/lib/ics.ts`
- Test: `frontend/lib/__tests__/ics.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// frontend/lib/__tests__/ics.test.ts
import { buildDeadlineIcs } from "@/lib/ics";

describe("buildDeadlineIcs", () => {
  it("produces a VCALENDAR with an all-day VEVENT on the deadline's date", async () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay the fee", documentName: "Tax notice.pdf" });
    const text = await blob.text();

    expect(text).toContain("BEGIN:VCALENDAR");
    expect(text).toContain("BEGIN:VEVENT");
    expect(text).toContain("DTSTART;VALUE=DATE:20260812");
    expect(text).toContain("DTEND;VALUE=DATE:20260813");
    expect(text).toContain("SUMMARY:Pay the fee (Tax notice.pdf)");
    expect(text).toContain("END:VEVENT");
    expect(text).toContain("END:VCALENDAR");
  });

  it("uses CRLF line endings per RFC 5545", async () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay", documentName: "x.pdf" });
    const text = await blob.text();
    expect(text).toContain("\r\n");
  });

  it("has a text/calendar MIME type", () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay", documentName: "x.pdf" });
    expect(blob.type).toBe("text/calendar");
  });

  it("escapes commas and semicolons in the summary per RFC 5545", async () => {
    const blob = buildDeadlineIcs({ date: "2026-08-12T00:00:00Z", description: "Pay, or appeal; see notice", documentName: "x.pdf" });
    const text = await blob.text();
    expect(text).toContain("Pay\\, or appeal\\; see notice");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest lib/__tests__/ics.test.ts`
Expected: FAIL — `Cannot find module '@/lib/ics'`

- [ ] **Step 3: Implement**

```ts
// frontend/lib/ics.ts
// Builds a minimal RFC 5545 .ics blob for a single all-day event, client-side, so "Add to
// calendar" needs no backend and no calendar-API permissions — it just hands the phone's own
// calendar app a file, which then owns reminders.

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

function dateStamp(d: Date): string {
  return `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}`;
}

function escapeText(s: string): string {
  return s.replace(/\\/g, "\\\\").replace(/,/g, "\\,").replace(/;/g, "\\;").replace(/\n/g, "\\n");
}

export function buildDeadlineIcs({
  date,
  description,
  documentName,
}: {
  date: string;
  description: string;
  documentName: string;
}): Blob {
  const start = new Date(date);
  const end = new Date(start);
  end.setUTCDate(end.getUTCDate() + 1);

  const uid = `${dateStamp(start)}-${Math.random().toString(36).slice(2)}@fana.app`;
  const summary = escapeText(`${description} (${documentName})`);

  const lines = [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Fana//Document Deadline//EN",
    "BEGIN:VEVENT",
    `UID:${uid}`,
    `DTSTAMP:${dateStamp(new Date())}T000000Z`,
    `DTSTART;VALUE=DATE:${dateStamp(start)}`,
    `DTEND;VALUE=DATE:${dateStamp(end)}`,
    `SUMMARY:${summary}`,
    "END:VEVENT",
    "END:VCALENDAR",
  ];

  return new Blob([lines.join("\r\n")], { type: "text/calendar" });
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest lib/__tests__/ics.test.ts`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/lib/ics.ts frontend/lib/__tests__/ics.test.ts
git commit -m "feat: add client-side .ics builder for deadline calendar export"
```

---

### Task 4: Wire checkboxes + "Add to calendar" into `AnalysisCard`

**Files:**
- Modify: `frontend/components/AnalysisCard.tsx`
- Modify: `frontend/components/__tests__/AnalysisCard.test.tsx`

Current actions block (`AnalysisCard.tsx:213-223`, already touched by the mandatory-action-chip plan — apply on top of that plan's result if it ran first):

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

Current deadlines block (`AnalysisCard.tsx:231-243`):

```tsx
        <CardContent>
          {analysis.deadlines.length === 0 ? (
            <p className="text-gray-500 dark:text-gray-400">—</p>
          ) : (
            <ul className="space-y-1 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.deadlines.map((d, i) => (
                <li key={i}>
                  {d.date ? <strong>{new Date(d.date).toLocaleDateString()}</strong> : null} {loc(d.description, language)}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
```

- [ ] **Step 1: Write the failing tests (append to `AnalysisCard.test.tsx`)**

```tsx
  it("persists a checked required action across remounts, keyed by documentId", () => {
    window.localStorage.clear();
    const { unmount } = render(
      <LanguageProvider><AnalysisCard analysis={ANALYSIS} documentId="doc-1" /></LanguageProvider>
    );
    fireEvent.click(screen.getAllByRole("checkbox")[0]);
    unmount();

    render(<LanguageProvider><AnalysisCard analysis={ANALYSIS} documentId="doc-1" /></LanguageProvider>);
    expect(screen.getAllByRole("checkbox")[0]).toBeChecked();
  });

  it("renders an 'Add to calendar' link for each dated deadline", () => {
    const withDeadline: AnalysisResult = {
      ...ANALYSIS,
      deadlines: [{ date: "2026-08-12T00:00:00Z", description: LOC("Payment due") }],
    };
    render(<LanguageProvider><AnalysisCard analysis={withDeadline} documentId="doc-1" /></LanguageProvider>);
    expect(screen.getByText("Add to calendar")).toBeInTheDocument();
  });
```

(Add `import { fireEvent } from "@testing-library/react";` to the existing import line if not already present, and remember `window.localStorage.clear()` needs `beforeEach` consistency with the file's existing tests — check whether one already exists before adding a duplicate.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/AnalysisCard.test.tsx`
Expected: FAIL — no `role="checkbox"` elements exist yet, no "Add to calendar" text.

- [ ] **Step 3: Implement**

Add imports:

```tsx
import { useState } from "react"; // already imported — merge into the existing React import line
import { CalendarPlus } from "lucide-react";
import { isActionChecked, setActionChecked } from "@/lib/actionProgress";
import { buildDeadlineIcs } from "@/lib/ics";
```

Add state near the top of the component body (after the existing `useState` calls):

```tsx
  const [, forceRerender] = useState(0);
```

(A tiny re-render trigger — `isActionChecked` reads localStorage directly rather than living in React state, since it must survive remounts across page navigations; toggling calls `setActionChecked` then forces a re-render to reflect the new checked state immediately.)

Replace the actions `<CardContent>`:

```tsx
          <CardContent>
            <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.requiredActions.map((a, i) => {
                const checked = documentId ? isActionChecked(documentId, i) : false;
                return (
                  <li key={i} className="flex items-start gap-2">
                    <input
                      type="checkbox"
                      checked={checked}
                      disabled={!documentId}
                      onChange={(e) => {
                        if (!documentId) return;
                        setActionChecked(documentId, i, e.target.checked);
                        forceRerender((n) => n + 1);
                      }}
                      aria-label={loc(a.description, language)}
                      className="mt-1 h-4 w-4 shrink-0 accent-brand"
                    />
                    {a.isMandatory && (
                      <>
                        <AlertTriangle aria-hidden="true" className="mt-1 h-4 w-4 shrink-0 text-orange-500" />
                        <span className="shrink-0 rounded-full bg-orange-100 px-2 py-0.5 text-xs font-medium text-orange-800 dark:bg-orange-900/40 dark:text-orange-300">
                          {t("doc.required")}
                        </span>
                      </>
                    )}
                    <span className={checked ? "line-through opacity-60" : undefined}>{loc(a.description, language)}</span>
                  </li>
                );
              })}
            </ul>
          </CardContent>
```

Replace the deadlines `<CardContent>`:

```tsx
        <CardContent>
          {analysis.deadlines.length === 0 ? (
            <p className="text-gray-500 dark:text-gray-400">—</p>
          ) : (
            <ul className="space-y-2 text-gray-700 dark:text-gray-300" dir={dir}>
              {analysis.deadlines.map((d, i) => (
                <li key={i} className="flex flex-wrap items-center gap-2">
                  <span>
                    {d.date ? <strong>{new Date(d.date).toLocaleDateString()}</strong> : null} {loc(d.description, language)}
                  </span>
                  {d.date && (
                    <a
                      href={URL.createObjectURL(buildDeadlineIcs({ date: d.date, description: loc(d.description, language), documentName: t("app.name") }))}
                      download={`deadline-${i}.ics`}
                      className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline"
                    >
                      <CalendarPlus className="h-4 w-4" />{t("doc.addToCalendar")}
                    </a>
                  )}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
```

- [ ] **Step 4: Add the `doc.addToCalendar` translation key to all three dictionaries**

Hebrew: `"doc.addToCalendar": "הוספה ליומן",`
Amharic: `"doc.addToCalendar": "ወደ ቀን መቁጠሪያ ጨምር",`
English: `"doc.addToCalendar": "Add to calendar",`

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/AnalysisCard.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/components/AnalysisCard.tsx frontend/components/__tests__/AnalysisCard.test.tsx frontend/i18n/dictionaries.ts
git commit -m "feat: persist required-action checkboxes and add per-deadline calendar export"
```

---

### Task 5: "Coming up" strip on the dashboard

**Files:**
- Create: `frontend/components/ComingUpStrip.tsx`
- Test: `frontend/components/__tests__/ComingUpStrip.test.tsx`
- Modify: `frontend/app/dashboard/page.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/ComingUpStrip.test.tsx
import { render, screen } from "@testing-library/react";
import { ComingUpStrip } from "@/components/ComingUpStrip";
import { LanguageProvider } from "@/lib/language-context";
import type { DocumentSummary } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });

function doc(id: string, fileName: string, deadlines: DocumentSummary["deadlines"]): DocumentSummary {
  return { id, fileName, contentType: "application/pdf", uploadedAt: new Date().toISOString(), hasAnalysis: true, status: 2, deadlines };
}

function renderStrip(docs: DocumentSummary[]) {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><ComingUpStrip docs={docs} /></LanguageProvider>);
}

describe("ComingUpStrip", () => {
  it("renders nothing when there are no upcoming deadlines", () => {
    const { container } = renderStrip([doc("d1", "a.pdf", [])]);
    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing when every deadline is in the past", () => {
    const { container } = renderStrip([doc("d1", "a.pdf", [{ date: "2000-01-01T00:00:00Z", description: LOC("Pay") }])]);
    expect(container).toBeEmptyDOMElement();
  });

  it("lists upcoming deadlines across documents, nearest first", () => {
    renderStrip([
      doc("d1", "Later.pdf", [{ date: "2999-06-01T00:00:00Z", description: LOC("Renew") }]),
      doc("d2", "Sooner.pdf", [{ date: "2999-01-01T00:00:00Z", description: LOC("Pay") }]),
    ]);
    const items = screen.getAllByRole("listitem");
    expect(items[0].textContent).toContain("Sooner.pdf");
    expect(items[1].textContent).toContain("Later.pdf");
  });

  it("links each entry to its document", () => {
    renderStrip([doc("d1", "a.pdf", [{ date: "2999-01-01T00:00:00Z", description: LOC("Pay") }])]);
    expect(screen.getByRole("link")).toHaveAttribute("href", "/documents/d1");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/ComingUpStrip.test.tsx`
Expected: FAIL — `Cannot find module '@/components/ComingUpStrip'`

- [ ] **Step 3: Implement**

```tsx
// frontend/components/ComingUpStrip.tsx
"use client";

import Link from "next/link";
import { CalendarClock } from "lucide-react";
import { useLanguage } from "@/lib/language-context";
import { loc, type DocumentSummary } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const MAX_SHOWN = 5;

/**
 * Flattens every document's deadlines into one nearest-first list, dropping anything already
 * past. Falls out of the DocumentSummary.deadlines DTO addition (see backend ListDocumentsHandler)
 * so no extra network round trip is needed beyond the dashboard's existing listDocuments() call.
 */
export function ComingUpStrip({ docs }: { docs: DocumentSummary[] }) {
  const { t, language, rtl } = useLanguage();
  const now = Date.now();

  const upcoming = docs
    .flatMap((d) => d.deadlines.map((dl) => ({ doc: d, deadline: dl })))
    .filter((x) => x.deadline.date && new Date(x.deadline.date).getTime() >= now)
    .sort((a, b) => new Date(a.deadline.date!).getTime() - new Date(b.deadline.date!).getTime())
    .slice(0, MAX_SHOWN);

  if (upcoming.length === 0) return null;

  return (
    <Card className="border-brand bg-brand-light dark:bg-brand/15">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg"><CalendarClock className="h-5 w-5 text-brand" />{t("dashboard.comingUp")}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="space-y-2" dir={rtl ? "rtl" : "ltr"}>
          {upcoming.map(({ doc, deadline }, i) => (
            <li key={`${doc.id}-${i}`}>
              <Link href={`/documents/${doc.id}`} className="flex flex-wrap items-center gap-2 hover:underline">
                <strong>{new Date(deadline.date!).toLocaleDateString(language)}</strong>
                <span>{loc(deadline.description, language)}</span>
                <span className="text-sm text-gray-500 dark:text-gray-400">— {doc.fileName}</span>
              </Link>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Add the `dashboard.comingUp` translation key to all three dictionaries**

Hebrew: `"dashboard.comingUp": "בקרוב",`
Amharic: `"dashboard.comingUp": "በቅርቡ",`
English: `"dashboard.comingUp": "Coming up",`

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/ComingUpStrip.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 6: Wire into the dashboard**

`dashboard/page.tsx` — add the import:

```tsx
import { ComingUpStrip } from "@/components/ComingUpStrip";
```

Insert `<ComingUpStrip docs={docs} />` right after the header row and before the `fetching`/empty/list conditional (i.e. between the closing `</div>` of the title+upload-button row at line 49 and the `{fetching ? (` at line 51):

```tsx
      </div>

      <ComingUpStrip docs={docs} />

      {fetching ? (
```

- [ ] **Step 7: Extend the dashboard test (from the confirm-dialog plan's Task 3) to cover this**

Add to `frontend/app/dashboard/__tests__/page.test.tsx`:

```tsx
  it("shows the Coming up strip when a document has an upcoming deadline", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([
      { ...DOC, deadlines: [{ date: "2999-01-01T00:00:00Z", description: { he: "x", am: "x", en: "Pay" } }] },
    ]);
    renderPage();
    await waitFor(() => screen.getByText("Coming up"));
  });
```

Also update the shared `DOC` fixture in that file to include `deadlines: []` so the existing tests (which don't care about deadlines) keep matching the real `DocumentSummary` shape.

- [ ] **Step 8: Run the full dashboard test file**

Run: `cd frontend && npx jest app/dashboard/__tests__/page.test.tsx`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add frontend/components/ComingUpStrip.tsx frontend/components/__tests__/ComingUpStrip.test.tsx frontend/app/dashboard/page.tsx frontend/app/dashboard/__tests__/page.test.tsx frontend/i18n/dictionaries.ts
git commit -m "feat: add Coming up strip listing nearest upcoming deadlines across all documents"
```

---

## Self-Review Notes

- **Spec coverage:** checkbox per required action, persisted, localStorage keyed by document id, same pattern as onboarding/trial ✓ Task 2+4; "Add to calendar" per dated deadline, client-side `.ics`, no backend/permissions ✓ Task 3+4; "Coming up" strip, falls out of a DTO change ✓ Task 1+5.
- **Edge case documented, not silently handled:** re-running analysis on a document resets checkbox progress, since actions are tracked by array index and the action list may change shape after re-analysis — called out in `actionProgress.ts`'s comment rather than solved (solving it would need the backend to assign stable per-action ids, which is out of scope for this ticket).
- **Trial analyses** (`documentId` undefined) get disabled, unchecked checkboxes — consistent with the fact that trial results aren't persisted anywhere yet (see the separate trial-analysis-persistence plan).
