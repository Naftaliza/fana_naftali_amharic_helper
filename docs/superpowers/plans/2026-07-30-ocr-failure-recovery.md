# OCR Failure Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When OCR fails, give the user real exits instead of a single red line and a "Cancel" that deletes the document: a localized, plain-language reason (raw error kept behind a details disclosure), a one-tap "Re-run OCR," a "Retake photo," and "Choose a file instead."

**Architecture:** Backend gets a small `RetryOcrCommand` that resets a Failed document back to Pending and re-enqueues it on the existing `IDocumentProcessingQueue` — the exact same primitive `DocumentProcessingWorker`'s crash-reconciliation path already uses, so no new processing machinery is needed. "Retake photo" and "Choose a file instead" can't replace an existing document's immutable pages, so both upload a **new** document and delete the failed one, then navigate to the new document — same two building blocks (`api.uploadDocument`, `api.deleteDocument`) already used elsewhere. A small pure mapper turns the raw `processingError` string into one of two known friendly messages (or a generic fallback), with the raw string always available behind a "Details" toggle.

**Tech Stack:** ASP.NET Core/MediatR + xUnit (backend), Next.js/React + Jest/RTL (frontend).

---

## File Structure

- Modify: `backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs` — add `RetryOcrCommand`/`RetryOcrHandler`.
- Modify: `backend/src/AmharicHelper.Api/Controllers/DocumentsController.cs` — add `POST /{id}/retry-ocr`.
- Create: `backend/tests/AmharicHelper.UnitTests/RetryOcrHandlerTests.cs`
- Modify: `frontend/lib/api.ts` — add `retryOcr`.
- Create: `frontend/lib/ocrErrors.ts` — raw-error-to-friendly-message mapper.
- Create: `frontend/lib/__tests__/ocrErrors.test.ts`
- Create: `frontend/components/OcrFailedCard.tsx`
- Create: `frontend/components/__tests__/OcrFailedCard.test.tsx`
- Modify: `frontend/app/documents/[id]/page.tsx` — replace the single-line Failed state with `OcrFailedCard`, restart polling after a successful retry.
- Modify: `frontend/i18n/dictionaries.ts` — add `doc.ocrFailedUnreadable`, `doc.ocrFailedService`, `doc.ocrDetails`, `doc.retryOcr`, `doc.retakePhoto`, `doc.ocrRetryError`.

---

### Task 1: Backend — `RetryOcrCommand`

**Files:**
- Modify: `backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs`
- Modify: `backend/src/AmharicHelper.Api/Controllers/DocumentsController.cs`
- Test: `backend/tests/AmharicHelper.UnitTests/RetryOcrHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/AmharicHelper.UnitTests/RetryOcrHandlerTests.cs
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class RetryOcrHandlerTests
{
    private sealed class FakeQueue : IDocumentProcessingQueue
    {
        public List<Guid> Enqueued { get; } = new();
        public void Enqueue(Guid documentId) => Enqueued.Add(documentId);
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? ToReturn { get; set; }
        public Document? Updated { get; private set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Document document, CancellationToken ct = default) { Updated = document; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Resets_a_failed_document_to_Pending_and_requeues_it()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = docId, UserId = userId, Status = DocumentProcessingStatus.Failed,
                ProcessingError = "No readable text was found in the document.", ProcessedPages = 2,
            },
        };
        var queue = new FakeQueue();
        var handler = new RetryOcrHandler(docs, queue);

        var result = await handler.Handle(new RetryOcrCommand(userId, docId), default);

        Assert.True(result.Success);
        Assert.Equal(DocumentProcessingStatus.Pending, docs.Updated!.Status);
        Assert.Null(docs.Updated.ProcessingError);
        Assert.Equal(0, docs.Updated.ProcessedPages);
        Assert.Single(queue.Enqueued);
        Assert.Equal(docId, queue.Enqueued[0]);
    }

    [Fact]
    public async Task Refuses_to_retry_a_document_that_is_not_Failed()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docs = new FakeDocs { ToReturn = new Document { Id = docId, UserId = userId, Status = DocumentProcessingStatus.Ready } };
        var handler = new RetryOcrHandler(docs, new FakeQueue());

        var result = await handler.Handle(new RetryOcrCommand(userId, docId), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Refuses_a_document_owned_by_someone_else()
    {
        var docId = Guid.NewGuid();
        var docs = new FakeDocs { ToReturn = new Document { Id = docId, UserId = Guid.NewGuid(), Status = DocumentProcessingStatus.Failed } };
        var handler = new RetryOcrHandler(docs, new FakeQueue());

        var result = await handler.Handle(new RetryOcrCommand(Guid.NewGuid(), docId), default);

        Assert.False(result.Success);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test --filter RetryOcrHandlerTests`
Expected: FAIL — `RetryOcrCommand`/`RetryOcrHandler` don't exist yet.

- [ ] **Step 3: Implement the command/handler**

Append to `backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs` (after the existing `AnalyzeDocumentHandler` class):

```csharp
// ---- Retry OCR (a Failed document only) ----
public record RetryOcrCommand(Guid UserId, Guid DocumentId) : IRequest<Result<bool>>;

public class RetryOcrHandler(
    IDocumentRepository documents,
    IDocumentProcessingQueue queue) : IRequestHandler<RetryOcrCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RetryOcrCommand cmd, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null || doc.UserId != cmd.UserId)
            return Result<bool>.Fail("Document not found.");
        if (doc.Status != DocumentProcessingStatus.Failed)
            return Result<bool>.Fail("Only a failed document can be retried.");

        // Same reset DocumentProcessor itself would see on a fresh upload — the in-memory queue
        // doesn't survive a restart (see DocumentProcessingWorker's own startup reconciliation,
        // which uses this identical Enqueue call), so explicitly re-queuing here is required.
        doc.Status = DocumentProcessingStatus.Pending;
        doc.ProcessedPages = 0;
        doc.ProcessingError = null;
        await documents.UpdateAsync(doc, ct);
        queue.Enqueue(doc.Id);

        return Result<bool>.Ok(true);
    }
}
```

Add the missing `using AmharicHelper.Domain.Enums;` at the top of `DocumentCommands.cs` if `DocumentProcessingStatus` isn't already in scope (check the existing usings — `AnalyzeDocumentHandler` already references `DocumentProcessingStatus.Pending` in this same file, so it's already imported).

- [ ] **Step 4: Add the controller route**

`DocumentsController.cs` — add after the `Analyze` action (~line 64):

```csharp
    /// <summary>Retry OCR on a document whose processing previously failed. Resets it to Pending
    /// and re-queues it on the same background pipeline as a fresh upload.</summary>
    [HttpPost("{id:guid}/retry-ocr")]
    public async Task<IActionResult> RetryOcr(Guid id)
    {
        var result = await Mediator.Send(new RetryOcrCommand(CurrentUserId, id));
        return result.Success ? Ok(new { ok = true }) : BadRequest(new { error = result.Error });
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd backend && dotnet test --filter RetryOcrHandlerTests`
Expected: PASS (3 tests)

- [ ] **Step 6: Run the full backend suite**

Run: `cd backend && dotnet test`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add backend/src/AmharicHelper.Application/Features/Documents/DocumentCommands.cs backend/src/AmharicHelper.Api/Controllers/DocumentsController.cs backend/tests/AmharicHelper.UnitTests/RetryOcrHandlerTests.cs
git commit -m "feat: add retry-ocr endpoint re-queuing a failed document"
```

---

### Task 2: Frontend — `api.retryOcr`

**Files:**
- Modify: `frontend/lib/api.ts`

- [ ] **Step 1: Add the client method**

Add next to `analyze` (`api.ts:168-169`):

```ts
  retryOcr: (id: string) =>
    request<{ ok: boolean }>(`/api/documents/${id}/retry-ocr`, { method: "POST" }),
```

- [ ] **Step 2: Commit**

```bash
git add frontend/lib/api.ts
git commit -m "feat: add api.retryOcr client method"
```

---

### Task 3: `lib/ocrErrors.ts` — friendly error mapper

**Files:**
- Create: `frontend/lib/ocrErrors.ts`
- Test: `frontend/lib/__tests__/ocrErrors.test.ts`

The only two `ProcessingError` strings `DocumentProcessor.cs` ever sets are `"No readable text was found in the document."` (`DocumentProcessor.cs:72`, every page came back blank/unreadable) and an `InvalidOperationException.Message` containing `"API key"` (`DocumentProcessor.cs:47-53`, a configuration error — internal/technical, not the user's fault, not something a retake fixes).

- [ ] **Step 1: Write the failing test**

```ts
// frontend/lib/__tests__/ocrErrors.test.ts
import { mapOcrError } from "@/lib/ocrErrors";

const t = (key: string) => ({
  "doc.ocrFailedUnreadable": "We couldn't read any text in this document.",
  "doc.ocrFailedService": "Something went wrong on our end.",
  "doc.ocrFailed": "We couldn't read this document.",
}[key] ?? key);

describe("mapOcrError", () => {
  it("maps the blank/unreadable-page message to a plain-language cause", () => {
    expect(mapOcrError("No readable text was found in the document.", t))
      .toBe("We couldn't read any text in this document.");
  });

  it("maps a configuration/API-key error to a generic service message, not a blur/lighting hint", () => {
    expect(mapOcrError("Missing Anthropic API key configuration.", t))
      .toBe("Something went wrong on our end.");
  });

  it("falls back to the generic ocrFailed message for anything unrecognized", () => {
    expect(mapOcrError("Some new backend error string", t)).toBe("We couldn't read this document.");
  });

  it("falls back to the generic message when there is no error at all", () => {
    expect(mapOcrError(null, t)).toBe("We couldn't read this document.");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest lib/__tests__/ocrErrors.test.ts`
Expected: FAIL — `Cannot find module '@/lib/ocrErrors'`

- [ ] **Step 3: Implement**

```ts
// frontend/lib/ocrErrors.ts
// Translates the raw backend processingError string (see DocumentProcessor.cs) into a plain-
// language cause a non-technical, possibly non-English-reading user can act on. The raw string
// itself is never shown as the primary message — it's always available behind a details toggle
// (see OcrFailedCard) for anyone who wants to report the exact error.

export function mapOcrError(raw: string | null, t: (key: string) => string): string {
  if (!raw) return t("doc.ocrFailed");
  if (raw.includes("No readable text")) return t("doc.ocrFailedUnreadable");
  if (raw.toLowerCase().includes("api key")) return t("doc.ocrFailedService");
  return t("doc.ocrFailed");
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest lib/__tests__/ocrErrors.test.ts`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/lib/ocrErrors.ts frontend/lib/__tests__/ocrErrors.test.ts
git commit -m "feat: map raw OCR processing errors to plain-language causes"
```

---

### Task 4: `doc.*` translation keys

**Files:**
- Modify: `frontend/i18n/dictionaries.ts`

- [ ] **Step 1: Add to the Hebrew block**

```ts
  "doc.ocrFailedUnreadable": "לא הצלחנו למצוא טקסט קריא במסמך. נסו לצלם שוב בתאורה טובה יותר, או לבחור קובץ אחר.",
  "doc.ocrFailedService": "משהו השתבש אצלנו. נסו שוב בעוד כמה דקות.",
  "doc.ocrDetails": "פרטים טכניים",
  "doc.retryOcr": "ניסיון חוזר",
  "doc.retakePhoto": "צילום מחדש",
  "doc.ocrRetryError": "הניסיון החוזר נכשל. נסו שוב.",
```

- [ ] **Step 2: Add to the Amharic block**

```ts
  "doc.ocrFailedUnreadable": "በሰነዱ ውስጥ ሊነበብ የሚችል ጽሁፍ አላገኘንም። በተሻለ ብርሃን እንደገና ፎቶ ያንሱ ወይም ሌላ ፋይል ይምረጡ።",
  "doc.ocrFailedService": "በኛ በኩል ችግር ተፈጥሯል። ከጥቂት ደቂቃዎች በኋላ እንደገና ይሞክሩ።",
  "doc.ocrDetails": "ቴክኒካዊ ዝርዝሮች",
  "doc.retryOcr": "እንደገና ሞክር",
  "doc.retakePhoto": "እንደገና ፎቶ አንሳ",
  "doc.ocrRetryError": "እንደገና መሞከር አልተሳካም። እባክዎ እንደገና ይሞክሩ።",
```

- [ ] **Step 3: Add to the English block**

```ts
  "doc.ocrFailedUnreadable": "We couldn't find any readable text in this document. Try photographing it again in better light, or choose a different file.",
  "doc.ocrFailedService": "Something went wrong on our end. Please try again in a few minutes.",
  "doc.ocrDetails": "Technical details",
  "doc.retryOcr": "Try again",
  "doc.retakePhoto": "Retake photo",
  "doc.ocrRetryError": "Retry failed. Please try again.",
```

- [ ] **Step 4: Commit**

```bash
git add frontend/i18n/dictionaries.ts
git commit -m "feat: add OCR failure recovery translation keys"
```

---

### Task 5: `OcrFailedCard` component

**Files:**
- Create: `frontend/components/OcrFailedCard.tsx`
- Test: `frontend/components/__tests__/OcrFailedCard.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/OcrFailedCard.test.tsx
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { OcrFailedCard } from "@/components/OcrFailedCard";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { uploadDocument: jest.fn(), deleteDocument: jest.fn() },
}));
const mockPush = jest.fn();
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: mockPush }) }));

function renderCard(props: Partial<React.ComponentProps<typeof OcrFailedCard>> = {}) {
  window.localStorage.setItem("lang", "en");
  const onRetryOcr = jest.fn();
  render(
    <LanguageProvider>
      <OcrFailedCard
        documentId="d1"
        rawError="No readable text was found in the document."
        onRetryOcr={onRetryOcr}
        retrying={false}
        {...props}
      />
    </LanguageProvider>
  );
  return { onRetryOcr };
}

describe("OcrFailedCard", () => {
  beforeEach(() => jest.clearAllMocks());

  it("shows the plain-language cause, not the raw backend string, as the primary message", () => {
    renderCard();
    expect(screen.getByRole("alert")).toHaveTextContent("We couldn't find any readable text");
    expect(screen.queryByText("No readable text was found in the document.")).not.toBeInTheDocument();
  });

  it("reveals the raw error behind a details toggle", () => {
    renderCard();
    fireEvent.click(screen.getByText("Technical details"));
    expect(screen.getByText("No readable text was found in the document.")).toBeInTheDocument();
  });

  it("calls onRetryOcr when 'Try again' is clicked", () => {
    const { onRetryOcr } = renderCard();
    fireEvent.click(screen.getByText("Try again"));
    expect(onRetryOcr).toHaveBeenCalledTimes(1);
  });

  it("uploads a replacement document and deletes the failed one when a file is chosen", async () => {
    (api.uploadDocument as jest.Mock).mockResolvedValue({ id: "new-doc" });
    (api.deleteDocument as jest.Mock).mockResolvedValue(undefined);
    renderCard();

    const file = new File(["x"], "retry.jpg", { type: "image/jpeg" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => expect(api.uploadDocument).toHaveBeenCalledWith([file]));
    await waitFor(() => expect(api.deleteDocument).toHaveBeenCalledWith("d1"));
    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/documents/new-doc"));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/OcrFailedCard.test.tsx`
Expected: FAIL — `Cannot find module '@/components/OcrFailedCard'`

- [ ] **Step 3: Implement**

```tsx
// frontend/components/OcrFailedCard.tsx
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/OcrFailedCard.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/components/OcrFailedCard.tsx frontend/components/__tests__/OcrFailedCard.test.tsx
git commit -m "feat: add OcrFailedCard with retry/retake/choose-file exits"
```

---

### Task 6: Wire into `documents/[id]/page.tsx`, restart polling after retry

**Files:**
- Modify: `frontend/app/documents/[id]/page.tsx`
- Test: `frontend/app/documents/[id]/__tests__/page.test.tsx` (created by the confirm-dialog plan's Task 4 — extend it; create fresh with the same mocking pattern if that plan hasn't run yet)

Current Failed-state block (`documents/[id]/page.tsx:142-148`):

```tsx
      {ocrFailed && (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            <p role="alert" className="text-red-600">{doc.processingError || t("doc.ocrFailed")}</p>
          </CardContent>
        </Card>
      )}
```

- [ ] **Step 1: Write the failing test**

```tsx
  it("shows OcrFailedCard with a plain-language cause when OCR failed", async () => {
    (api.getDocument as jest.Mock).mockResolvedValue({
      ...PENDING_DOC, status: 3, processingError: "No readable text was found in the document.",
    });
    renderPage();
    await waitFor(() => screen.getByRole("alert"));
    expect(screen.getByRole("alert")).toHaveTextContent(/couldn't find any readable text/i);
    expect(screen.getByText("Retake photo")).toBeInTheDocument();
  });

  it("re-runs OCR and resumes polling when 'Try again' is clicked", async () => {
    (api.getDocument as jest.Mock)
      .mockResolvedValueOnce({ ...PENDING_DOC, status: 3, processingError: "No readable text was found in the document." })
      .mockResolvedValueOnce({ ...PENDING_DOC, status: 0 })
      .mockResolvedValueOnce({ ...PENDING_DOC, status: 2, analysis: null });
    (api.retryOcr as jest.Mock).mockResolvedValue({ ok: true });
    renderPage();
    await waitFor(() => screen.getByText("Try again"));

    fireEvent.click(screen.getByText("Try again"));

    await waitFor(() => expect(api.retryOcr).toHaveBeenCalledWith("d1"));
    await waitFor(() => expect(api.getDocument).toHaveBeenCalledTimes(2), { timeout: 3000 });
  });
```

Add `retryOcr: jest.fn()` to the `jest.mock("@/lib/api", ...)` factory at the top of the file.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest "app/documents/[id]/__tests__/page.test.tsx"`
Expected: FAIL — the current Failed block shows the raw `processingError` string directly, with no "Retake photo" or "Try again" button.

- [ ] **Step 3: Implement**

Add the import:

```tsx
import { OcrFailedCard } from "@/components/OcrFailedCard";
```

Add a `retryKey`/`retryingOcr` state and a `retryOcr` handler. The polling `useEffect` (`documents/[id]/page.tsx:63-91`) needs to re-run after a retry, so add `retryKey` to its dependency array:

```tsx
  const [retryKey, setRetryKey] = useState(0);
  const [retryingOcr, setRetryingOcr] = useState(false);

  const retryOcr = async () => {
    setRetryingOcr(true);
    try {
      await api.retryOcr(id);
      startedRef.current = false;
      setDoc(null); // show the loading state while polling restarts
      setRetryKey((k) => k + 1);
    } catch (err) {
      setError((err as Error).message || t("doc.ocrRetryError"));
    } finally {
      setRetryingOcr(false);
    }
  };
```

Change the polling effect's dependency array from `[id]` to `[id, retryKey]` (the effect body itself is unchanged — it already re-fetches from scratch each time it runs).

Replace the Failed-state JSX:

```tsx
      {ocrFailed && (
        <OcrFailedCard
          documentId={id}
          rawError={doc.processingError}
          onRetryOcr={retryOcr}
          retrying={retryingOcr}
        />
      )}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest "app/documents/[id]/__tests__/page.test.tsx"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add "frontend/app/documents/[id]/page.tsx" "frontend/app/documents/[id]/__tests__/page.test.tsx"
git commit -m "feat: replace single-line OCR failure message with retry/retake/choose-file recovery"
```

---

## Self-Review Notes

- **Spec coverage:** processingError mapped to localized plain-language causes, raw string behind a details disclosure ✓ Task 3+5; real exits — re-run OCR (backend re-enqueue reusing the existing crash-reconciliation primitive) ✓ Task 1+6, retake photo (reopens CameraCapture) ✓ Task 5, choose a file instead ✓ Task 5; pages already visible via DocumentPages during this state — unchanged, no action needed since `DocumentPages` renders independently of the Failed branch (`documents/[id]/page.tsx:129-131`, gated on `status !== Pending && totalPages > 0`, which is already true once a document reaches Failed).
- **Not duplicated:** `Cancel` (delete-outright) already exists as an escape hatch and is untouched by this plan — it remains available for a user who just wants to abandon the document entirely rather than retry/replace it.
