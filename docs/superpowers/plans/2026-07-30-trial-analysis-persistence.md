# Trial Analysis Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop destroying the anonymous trial analysis at the exact moment the user taps "register" — stash it before navigating away, and offer to save it to the new account once email verification completes.

**Architecture:** The trial result (`AnalysisResult`) is JSON-serializable, so it's stashed in `localStorage` right before navigating to `/register` — same typed-blob pattern as `lib/leadFeedback.ts`. There is no original OCR text or page image available to re-attach (the anonymous trial endpoint never persists either), so "saving" it means a new small backend endpoint that creates a document from the already-computed analysis alone — no file, no OCR. After verification redirects to the dashboard, a global prompt (mirroring the existing `LeadFeedbackPrompt` singleton) offers to save it, calls the endpoint, and clears the stash either way once handled.

**Tech Stack:** ASP.NET Core/MediatR + xUnit (backend), Next.js/React + Jest/RTL (frontend).

**Dependency note:** This plan's backend task constructs `DocumentSummaryDto` with a `Deadlines` field, added by the separate `2026-07-30-deadline-action-persistence.md` plan (Task 1). Build that plan first. If this plan must run standalone, drop the `Deadlines` argument from the `DocumentSummaryDto` construction in Task 1 below and pass whatever that plan's actual current constructor shape requires.

---

## File Structure

- Modify: `backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs` — add `AttachTrialAnalysisCommand`/`AttachTrialAnalysisHandler`.
- Modify: `backend/src/AmharicHelper.Api/Controllers/DocumentsController.cs` — add `POST /api/documents/attach-trial`.
- Create: `backend/tests/AmharicHelper.UnitTests/AttachTrialAnalysisHandlerTests.cs`
- Modify: `frontend/lib/api.ts` — add `attachTrialAnalysis`.
- Create: `frontend/lib/pendingTrialAnalysis.ts` — stash/get/clear, copying `lib/leadFeedback.ts`.
- Create: `frontend/lib/__tests__/pendingTrialAnalysis.test.ts`
- Modify: `frontend/components/UploadExperience.tsx` — stash before navigating to `/register`.
- Create: `frontend/components/SaveTrialAnalysisPrompt.tsx` — mirrors `LeadFeedbackPrompt.tsx`.
- Create: `frontend/components/__tests__/SaveTrialAnalysisPrompt.test.tsx`
- Modify: `frontend/app/layout.tsx` — render the new prompt alongside `LeadFeedbackPrompt`.
- Modify: `frontend/i18n/dictionaries.ts` — add `trial.attachPrompt`, `trial.attachSave`, `trial.attachSaved`, `trial.attachError`.

---

### Task 1: Backend — `AttachTrialAnalysisCommand`

**Files:**
- Modify: `backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs`
- Modify: `backend/src/AmharicHelper.Api/Controllers/DocumentsController.cs`
- Test: `backend/tests/AmharicHelper.UnitTests/AttachTrialAnalysisHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/AmharicHelper.UnitTests/AttachTrialAnalysisHandlerTests.cs
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class AttachTrialAnalysisHandlerTests
{
    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? Added { get; private set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Document?>(null);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) { Added = document; return Task.CompletedTask; }
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAnalyses : IDocumentAnalysisRepository
    {
        public DocumentAnalysis? Added { get; private set; }
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult<DocumentAnalysis?>(null);
        public Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default) { Added = analysis; return Task.CompletedTask; }
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Creates_a_Ready_document_with_no_pages_from_the_trial_analysis()
    {
        var userId = Guid.NewGuid();
        var docs = new FakeDocs();
        var analyses = new FakeAnalyses();
        var handler = new AttachTrialAnalysisHandler(docs, analyses);

        var analysis = new DocumentAnalysisResult
        {
            Summary = new LocalizedText("תקציר", "ማጠቃለያ", "Summary"),
            DocumentType = new LocalizedText("מכתב", "ደብዳቤ", "Letter"),
            UrgencyLevel = UrgencyLevel.Medium,
            KeyPoints = new() { new LocalizedText("א", "ሀ", "Point") },
            RequiredActions = new() { new RequiredActionDto { Description = new LocalizedText("שלמו", "ይክፈሉ", "Pay"), IsMandatory = true } },
            Deadlines = new() { new DeadlineDto { Date = new DateTime(2026, 8, 12), Description = new LocalizedText("שלמו", "ይክፈሉ", "Pay") } },
            Explanation = new LocalizedText("הסבר", "ማብራሪያ", "Explanation"),
        };

        var result = await handler.Handle(new AttachTrialAnalysisCommand(userId, analysis), default);

        Assert.True(result.Success);
        Assert.Equal(userId, docs.Added!.UserId);
        Assert.Equal(DocumentProcessingStatus.Ready, docs.Added.Status);
        Assert.Empty(docs.Added.PagePaths);
        Assert.Equal(0, docs.Added.TotalPages);
        Assert.Null(docs.Added.OcrText);

        Assert.Equal(docs.Added.Id, analyses.Added!.DocumentId);
        Assert.Equal("Pay", analyses.Added.RequiredActions[0].Description.En);
        Assert.Single(analyses.Added.Deadlines);

        Assert.True(result.Value!.HasAnalysis);
        Assert.Equal(docs.Added.Id, result.Value.Id);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test --filter AttachTrialAnalysisHandlerTests`
Expected: FAIL — `AttachTrialAnalysisCommand`/`AttachTrialAnalysisHandler` don't exist yet.

- [ ] **Step 3: Implement**

Append to `backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs`:

```csharp
// ---- Attach a previously-computed anonymous trial analysis to the now-authenticated account.
// No OCR text or page files exist for a trial result (the anonymous /api/trial/analyze endpoint
// never persists either) — this only saves the analysis itself, so the user keeps the
// explanation/actions/deadlines instead of losing it entirely at the signup moment. ----
public record AttachTrialAnalysisCommand(Guid UserId, DocumentAnalysisResult Analysis)
    : IRequest<Result<DocumentSummaryDto>>;

public class AttachTrialAnalysisHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses) : IRequestHandler<AttachTrialAnalysisCommand, Result<DocumentSummaryDto>>
{
    public async Task<Result<DocumentSummaryDto>> Handle(AttachTrialAnalysisCommand cmd, CancellationToken ct)
    {
        var typeLabel = cmd.Analysis.DocumentType.En;
        var doc = new Document
        {
            UserId = cmd.UserId,
            FileName = string.IsNullOrWhiteSpace(typeLabel) ? "Saved analysis" : typeLabel,
            ContentType = "application/octet-stream",
            PagePaths = Array.Empty<string>(),
            PageContentTypes = Array.Empty<string>(),
            Status = DocumentProcessingStatus.Ready,
            TotalPages = 0,
        };
        await documents.AddAsync(doc, ct);

        await analyses.AddAsync(new DocumentAnalysis
        {
            DocumentId = doc.Id,
            Summary = cmd.Analysis.Summary,
            DocumentType = cmd.Analysis.DocumentType,
            Category = cmd.Analysis.Category,
            UrgencyLevel = cmd.Analysis.UrgencyLevel,
            KeyPoints = cmd.Analysis.KeyPoints.ToList(),
            RequiredActions = cmd.Analysis.RequiredActions
                .Select(a => new RequiredAction(a.Description, a.IsMandatory)).ToList(),
            Deadlines = cmd.Analysis.Deadlines
                .Select(d => new Deadline(d.Date, d.Description)).ToList(),
            Explanation = cmd.Analysis.Explanation,
        }, ct);

        return Result<DocumentSummaryDto>.Ok(new DocumentSummaryDto(
            doc.Id, doc.FileName, doc.ContentType, doc.UploadedAt, true, doc.Status, cmd.Analysis.Deadlines));
    }
}
```

- [ ] **Step 4: Add the controller route**

`DocumentsController.cs` — add near the top, after `List()` (~line 42), since it doesn't take a document id:

```csharp
    /// <summary>Saves a previously-computed anonymous trial analysis to the now-authenticated
    /// user's account. There is no OCR text or page file for a trial result — only the analysis
    /// itself is persisted.</summary>
    [HttpPost("attach-trial")]
    public async Task<IActionResult> AttachTrial(DocumentAnalysisResult analysis)
    {
        var result = await Mediator.Send(new AttachTrialAnalysisCommand(CurrentUserId, analysis));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test --filter AttachTrialAnalysisHandlerTests`
Expected: PASS

- [ ] **Step 6: Run the full backend suite**

Run: `cd backend && dotnet test`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs backend/src/AmharicHelper.Api/Controllers/DocumentsController.cs backend/tests/AmharicHelper.UnitTests/AttachTrialAnalysisHandlerTests.cs
git commit -m "feat: add endpoint to attach a saved trial analysis to an account"
```

---

### Task 2: Frontend — `api.attachTrialAnalysis` + stash module

**Files:**
- Modify: `frontend/lib/api.ts`
- Create: `frontend/lib/pendingTrialAnalysis.ts`
- Test: `frontend/lib/__tests__/pendingTrialAnalysis.test.ts`

- [ ] **Step 1: Add the client method**

`api.ts` — add near `trialAnalyze` (`api.ts:172-176`):

```ts
  attachTrialAnalysis: (analysis: AnalysisResult) =>
    request<DocumentSummary>("/api/documents/attach-trial", { method: "POST", body: JSON.stringify(analysis) }),
```

- [ ] **Step 2: Write the failing test for the stash module**

```ts
// frontend/lib/__tests__/pendingTrialAnalysis.test.ts
import { getPendingTrialAnalysis, setPendingTrialAnalysis, clearPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import type { AnalysisResult } from "@/lib/types";

const LOC = (s: string) => ({ he: s, am: s, en: s });
const ANALYSIS: AnalysisResult = {
  summary: LOC("s"), documentType: LOC("t"), urgencyLevel: "Low",
  keyPoints: [], requiredActions: [], deadlines: [], explanation: LOC("e"),
};

describe("pendingTrialAnalysis", () => {
  beforeEach(() => window.localStorage.clear());

  it("returns null when nothing has been stashed", () => {
    expect(getPendingTrialAnalysis()).toBeNull();
  });

  it("round-trips a stashed analysis", () => {
    setPendingTrialAnalysis(ANALYSIS);
    const pending = getPendingTrialAnalysis();
    expect(pending?.analysis).toEqual(ANALYSIS);
    expect(typeof pending?.stashedAt).toBe("number");
  });

  it("clears the stash", () => {
    setPendingTrialAnalysis(ANALYSIS);
    clearPendingTrialAnalysis();
    expect(getPendingTrialAnalysis()).toBeNull();
  });

  it("tolerates corrupted storage instead of throwing", () => {
    window.localStorage.setItem("pendingTrialAnalysis", "{not json");
    expect(getPendingTrialAnalysis()).toBeNull();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd frontend && npx jest lib/__tests__/pendingTrialAnalysis.test.ts`
Expected: FAIL — `Cannot find module '@/lib/pendingTrialAnalysis'`

- [ ] **Step 4: Implement**

```ts
// frontend/lib/pendingTrialAnalysis.ts
// Stashes the anonymous trial analysis just before navigating to /register, so the single
// highest-intent moment in the funnel (tapping "register" right after reading a good analysis)
// doesn't destroy it. Same typed-blob localStorage pattern as lib/leadFeedback.ts.

import type { AnalysisResult } from "@/lib/types";

const KEY = "pendingTrialAnalysis";

export interface PendingTrialAnalysis {
  analysis: AnalysisResult;
  stashedAt: number;
}

export function setPendingTrialAnalysis(analysis: AnalysisResult): void {
  window.localStorage.setItem(KEY, JSON.stringify({ analysis, stashedAt: Date.now() }));
}

export function getPendingTrialAnalysis(): PendingTrialAnalysis | null {
  if (typeof window === "undefined") return null;
  try {
    return JSON.parse(window.localStorage.getItem(KEY) ?? "null");
  } catch {
    return null;
  }
}

export function clearPendingTrialAnalysis(): void {
  window.localStorage.removeItem(KEY);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx jest lib/__tests__/pendingTrialAnalysis.test.ts`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add frontend/lib/api.ts frontend/lib/pendingTrialAnalysis.ts frontend/lib/__tests__/pendingTrialAnalysis.test.ts
git commit -m "feat: add pendingTrialAnalysis stash and api.attachTrialAnalysis"
```

---

### Task 3: `trial.attach*` translation keys

**Files:**
- Modify: `frontend/i18n/dictionaries.ts`

- [ ] **Step 1: Add to the Hebrew block**

```ts
  "trial.attachPrompt": "לשמור את המסמך שניתחתם לפני ההרשמה?",
  "trial.attachSave": "שמירה",
  "trial.attachSaved": "נשמר! תמצאו אותו בלוח הבקרה.",
  "trial.attachError": "השמירה נכשלה. אפשר לנסות שוב מאוחר יותר.",
```

- [ ] **Step 2: Add to the Amharic block**

```ts
  "trial.attachPrompt": "ከመመዝገብዎ በፊት የተነተኑትን ሰነድ ማስቀመጥ ይፈልጋሉ?",
  "trial.attachSave": "አስቀምጥ",
  "trial.attachSaved": "ተቀምጧል! በዳሽቦርድዎ ላይ ያገኙታል።",
  "trial.attachError": "ማስቀመጥ አልተሳካም። ቆይተው እንደገና ይሞክሩ።",
```

- [ ] **Step 3: Add to the English block**

```ts
  "trial.attachPrompt": "Save the document you analyzed before signing up?",
  "trial.attachSave": "Save",
  "trial.attachSaved": "Saved! You'll find it on your dashboard.",
  "trial.attachError": "Save failed. You can try again later.",
```

- [ ] **Step 4: Commit**

```bash
git add frontend/i18n/dictionaries.ts
git commit -m "feat: add trial.attach* translation keys"
```

---

### Task 4: Stash before navigating to `/register`

**Files:**
- Modify: `frontend/components/UploadExperience.tsx`
- Test: `frontend/components/__tests__/UploadExperience.test.tsx` (created by the offline-handling plan — extend it; create fresh with the same mocking pattern shown there if that plan hasn't run yet)

Current code (`UploadExperience.tsx:104-115`):

```tsx
            {!user && <p className="text-lg font-medium">{t("trial.savePrompt").replace("{n}", String(remaining))}</p>}
            <div className="flex flex-wrap justify-center gap-3">
              {!user && <Link href="/register"><Button>{t("nav.register")}</Button></Link>}
              <Button
                variant="outline"
                onClick={() => { setTrialResult(null); setError(null); }}
                disabled={!user && remaining <= 0}
              >
                {t("trial.tryAnother")}
              </Button>
              <ShareButton />
            </div>
```

- [ ] **Step 1: Write the failing test**

```tsx
// append to frontend/components/__tests__/UploadExperience.test.tsx
import { getPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
// (add to the existing import block if the file already imports from "@testing-library/react"/etc.)

describe("UploadExperience trial persistence", () => {
  afterEach(() => window.localStorage.clear());

  it("stashes the trial analysis before navigating to register", async () => {
    jest.mock("@/lib/api", () => ({
      api: {
        trialAnalyze: jest.fn().mockResolvedValue({
          summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "t" },
          urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
          explanation: { he: "e", am: "e", en: "e" },
        }),
      },
    }));
    // NOTE: this test needs the component to already be showing a trialResult. The cleanest way
    // is to drive it through the real upload flow (mock api.trialAnalyze, upload a file, wait for
    // the result panel), then click the register link and assert the stash. See the existing
    // handleFiles tests elsewhere in this file (if any) for the exact file-upload-trigger pattern;
    // if none exist yet, trigger via the hidden file input the same way OcrFailedCard's test does:
    // fireEvent.change(document.querySelector('input[type="file"]'), { target: { files: [file] } }).
  });
});
```

Given the file-upload round trip needed to reach the `trialResult` render branch is involved, write this as a more direct test instead: extract nothing new — just drive the real flow.

```tsx
// frontend/components/__tests__/UploadExperience.test.tsx (replace the placeholder above with this)
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { UploadExperience } from "@/components/UploadExperience";
import { LanguageProvider } from "@/lib/language-context";
import { OrganizationProvider } from "@/lib/organization-context";
import { getPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({ api: { trialAnalyze: jest.fn() } }));
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
jest.mock("@/lib/auth-context", () => ({ useAuth: () => ({ user: null, loading: false }) }));

const ANALYSIS = {
  summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "Letter" },
  urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
  explanation: { he: "e", am: "e", en: "e" },
};

function renderExperience() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider><OrganizationProvider><UploadExperience /></OrganizationProvider></LanguageProvider>
  );
}

describe("UploadExperience trial persistence", () => {
  beforeEach(() => { jest.clearAllMocks(); window.localStorage.removeItem("pendingTrialAnalysis"); });

  it("stashes the trial analysis when the register link is clicked", async () => {
    (api.trialAnalyze as jest.Mock).mockResolvedValue(ANALYSIS);
    renderExperience();

    const file = new File(["x"], "doc.jpg", { type: "image/jpeg" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => screen.getByText("Letter"));

    fireEvent.click(screen.getByText("Register"));

    expect(getPendingTrialAnalysis()?.analysis).toEqual(ANALYSIS);
  });
});
```

(Check the real English string for `nav.register` before finalizing the `getByText("Register")` match — adjust to whatever `dictionaries.ts`'s `en` block actually has.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/UploadExperience.test.tsx`
Expected: FAIL — nothing is stashed today; `getPendingTrialAnalysis()` returns `null`.

- [ ] **Step 3: Implement**

Add the import:

```tsx
import { setPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
```

Change the register link (`UploadExperience.tsx:106`):

```tsx
              {!user && (
                <Link href="/register" onClick={() => setPendingTrialAnalysis(trialResult)}>
                  <Button>{t("nav.register")}</Button>
                </Link>
              )}
```

(`trialResult` is guaranteed non-null here — this whole branch only renders `if (trialResult) { ... }`, see `UploadExperience.tsx:98`.)

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/UploadExperience.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/components/UploadExperience.tsx frontend/components/__tests__/UploadExperience.test.tsx
git commit -m "feat: stash the trial analysis before navigating to register"
```

---

### Task 5: `SaveTrialAnalysisPrompt` global singleton

**Files:**
- Create: `frontend/components/SaveTrialAnalysisPrompt.tsx`
- Test: `frontend/components/__tests__/SaveTrialAnalysisPrompt.test.tsx`
- Modify: `frontend/app/layout.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/SaveTrialAnalysisPrompt.test.tsx
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SaveTrialAnalysisPrompt } from "@/components/SaveTrialAnalysisPrompt";
import { LanguageProvider } from "@/lib/language-context";
import { setPendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({ api: { attachTrialAnalysis: jest.fn() } }));

let mockUser: { id: string } | null = null;
jest.mock("@/lib/auth-context", () => ({ useAuth: () => ({ user: mockUser, loading: false }) }));

const ANALYSIS = {
  summary: { he: "s", am: "s", en: "s" }, documentType: { he: "t", am: "t", en: "t" },
  urgencyLevel: "Low", keyPoints: [], requiredActions: [], deadlines: [],
  explanation: { he: "e", am: "e", en: "e" },
};

function renderPrompt() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><SaveTrialAnalysisPrompt /></LanguageProvider>);
}

describe("SaveTrialAnalysisPrompt", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    window.localStorage.removeItem("pendingTrialAnalysis");
    mockUser = null;
  });

  it("renders nothing when signed out, even with a stash present", () => {
    setPendingTrialAnalysis(ANALYSIS as any);
    const { container } = renderPrompt();
    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing when signed in with no stash", () => {
    mockUser = { id: "u1" };
    const { container } = renderPrompt();
    expect(container).toBeEmptyDOMElement();
  });

  it("offers to save when signed in with a stash present", () => {
    mockUser = { id: "u1" };
    setPendingTrialAnalysis(ANALYSIS as any);
    renderPrompt();
    expect(screen.getByText("Save the document you analyzed before signing up?")).toBeInTheDocument();
  });

  it("saves and clears the stash on confirm", async () => {
    mockUser = { id: "u1" };
    setPendingTrialAnalysis(ANALYSIS as any);
    (api.attachTrialAnalysis as jest.Mock).mockResolvedValue({ id: "d1" });
    renderPrompt();

    fireEvent.click(screen.getByText("Save"));

    await waitFor(() => expect(api.attachTrialAnalysis).toHaveBeenCalledWith(ANALYSIS));
    expect(window.localStorage.getItem("pendingTrialAnalysis")).toBeNull();
  });

  it("dismissing clears the stash without saving", () => {
    mockUser = { id: "u1" };
    setPendingTrialAnalysis(ANALYSIS as any);
    renderPrompt();

    fireEvent.click(screen.getByLabelText("Skip"));

    expect(api.attachTrialAnalysis).not.toHaveBeenCalled();
    expect(window.localStorage.getItem("pendingTrialAnalysis")).toBeNull();
  });
});
```

(Check the real English value of `onboarding.skip` — reused here for the dismiss button's aria-label, same as `LeadFeedbackPrompt` does — before finalizing `getByLabelText("Skip")`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/SaveTrialAnalysisPrompt.test.tsx`
Expected: FAIL — `Cannot find module '@/components/SaveTrialAnalysisPrompt'`

- [ ] **Step 3: Implement**

```tsx
// frontend/components/SaveTrialAnalysisPrompt.tsx
"use client";

import { useEffect, useState } from "react";
import { FileCheck2, X } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { clearPendingTrialAnalysis, getPendingTrialAnalysis, type PendingTrialAnalysis } from "@/lib/pendingTrialAnalysis";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

/**
 * Offers to save a trial analysis stashed just before the user tapped "register" (see
 * UploadExperience). Only makes sense once signed in — mirrors LeadFeedbackPrompt's global-
 * singleton placement in layout.tsx and its dismiss/backdrop styling.
 */
export function SaveTrialAnalysisPrompt() {
  const { user } = useAuth();
  const { t, rtl } = useLanguage();
  const [pending, setPending] = useState<PendingTrialAnalysis | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user) setPending(getPendingTrialAnalysis());
  }, [user]);

  if (!user || !pending) return null;

  const dismiss = () => {
    clearPendingTrialAnalysis();
    setPending(null);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.attachTrialAnalysis(pending.analysis);
      setSaved(true);
      clearPendingTrialAnalysis();
      window.setTimeout(() => setPending(null), 1500);
    } catch {
      setError(t("trial.attachError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      dir={rtl ? "rtl" : "ltr"}
      className="pb-safe fixed inset-x-4 bottom-4 z-50 mx-auto max-w-sm sm:inset-x-auto sm:start-4"
    >
      <Card className="shadow-soft">
        <CardContent className="relative py-4">
          <button
            onClick={dismiss}
            aria-label={t("onboarding.skip")}
            className="absolute end-3 top-3 rounded-lg p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <X className="h-4 w-4" />
          </button>
          {saved ? (
            <p className="pe-6 text-sm font-medium text-brand">{t("trial.attachSaved")}</p>
          ) : (
            <>
              <p className="pe-6 text-sm font-medium">{t("trial.attachPrompt")}</p>
              {error && <p role="alert" className="mt-1 text-xs text-red-600">{error}</p>}
              <Button onClick={save} disabled={saving} className="mt-3 w-full">
                <FileCheck2 className="h-4 w-4" />{saving ? t("common.loading") : t("trial.attachSave")}
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/SaveTrialAnalysisPrompt.test.tsx`
Expected: PASS (5 tests)

- [ ] **Step 5: Wire into `layout.tsx`**

Add the import and render it next to `LeadFeedbackPrompt` (`layout.tsx:71`):

```tsx
import { SaveTrialAnalysisPrompt } from "@/components/SaveTrialAnalysisPrompt";
// ...
                  <LeadFeedbackPrompt />
                  <SaveTrialAnalysisPrompt />
```

(The two prompts share the same bottom-anchored positioning; in the rare case both are pending simultaneously they'd stack — acceptable, since each is dismissible and this is an edge case, not a common path.)

- [ ] **Step 6: Commit**

```bash
git add frontend/components/SaveTrialAnalysisPrompt.tsx frontend/components/__tests__/SaveTrialAnalysisPrompt.test.tsx frontend/app/layout.tsx
git commit -m "feat: offer to save a stashed trial analysis once signed in"
```

---

## Self-Review Notes

- **Spec coverage:** stash before navigating, same pattern as `lib/leadFeedback.ts` ✓ Task 2+4; offer "Save the document you just analyzed" once verification lands the user in the dashboard ✓ Task 5 (the prompt is a global singleton, so it naturally appears the moment `user` becomes truthy post-verification, without needing to special-case the verify-email redirect itself); attach via a small backend endpoint since the original files can't be re-uploaded across an email-link round trip (different tab/device is common) ✓ Task 1.
- **Deliberate scope reduction:** the ticket offers two options for "save" — a small attach endpoint, or re-uploading the retained page files. This plan takes the attach-endpoint path only; re-uploading files was ruled out because a verification link is routinely opened in a different tab or even a different device (checking email on a phone after uploading from a shared computer, etc.), where no in-memory `File` objects survive at all — an endpoint needing only the already-computed analysis is the only version of this feature that works unconditionally.
- **Known limitation, not silently hidden:** an attached trial document has no OCR text and no viewable page images (`DocumentPages` will show nothing for it) — only the analysis. This is inherent to what data a trial run ever produces on the client, not a bug; if a future ticket wants the original photos preserved too, the trial upload flow itself would need to keep the `File` objects in memory across the `/register` navigation, which crosses a full page reload and is a materially different (and much harder) change.
- **Ticket's stated fallback** ("at minimum keep the result rendered behind the signup prompt so it isn't lost on a mistap") is already true today and unaffected by this plan: `trialResult` stays rendered in `UploadExperience` until the user actually clicks a link that unmounts it — this plan's fix is specifically for the moment of navigation itself, not a mistap while still on the page.
