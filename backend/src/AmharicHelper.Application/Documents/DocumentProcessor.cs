using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Documents;

/// <summary>
/// Runs the OCR pipeline for one already-uploaded document, page by page, persisting progress
/// after every page so the client can poll real status instead of guessing at a timer. Invoked by
/// DocumentProcessingWorker (Infrastructure) off the request thread; also called directly by
/// tests, so it has no dependency on any particular hosting model.
///
/// Failure policy (carried over from the previous synchronous MultiPageOcr):
/// - A page "succeeds" only if OCR returns non-whitespace text. Claude returns an empty string for
///   a blank/unreadable page (it does not throw), so empty == skipped.
/// - A page-level OCR exception (e.g. a transient failure that survived retries) skips that page
///   but the document continues.
/// - A configuration/auth error (missing API key) or an account-level OCR failure (quota exceeded,
///   rate limited, service outage) would fail every remaining page identically, so it aborts the
///   whole document as Failed with an accurate message rather than silently skipping every page
///   and reporting the misleading "no readable text was found".
/// - If no page yields text, the document is marked Failed.
/// </summary>
public class DocumentProcessor(IDocumentRepository documents, IFileStorage storage, IOcrProvider ocr)
{
    public async Task ProcessAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(documentId, ct);
        // Already finished (duplicate enqueue, or re-queued by startup reconciliation after it had
        // already completed) or deleted while queued — nothing to do either way.
        if (doc is null || doc.Status is DocumentProcessingStatus.Ready or DocumentProcessingStatus.Failed)
            return;

        doc.Status = DocumentProcessingStatus.Processing;
        await documents.UpdateAsync(doc, ct);

        var keptTexts = new List<string>();

        // Sequential on purpose: the Anthropic client only retries a few times on 429, so firing
        // every page in parallel would collide with rate limits.
        for (var i = 0; i < doc.PagePaths.Length; i++)
        {
            string text;
            try
            {
                var bytes = await storage.ReadAsync(doc.PagePaths[i], ct);
                var contentType = i < doc.PageContentTypes.Length ? doc.PageContentTypes[i] : doc.ContentType;
                text = await ocr.ExtractTextAsync(bytes, contentType, ct);
            }
            catch (InvalidOperationException ex) when (IsFatalError(ex))
            {
                doc.Status = DocumentProcessingStatus.Failed;
                doc.ProcessingError = ex.Message;
                doc.ProcessedPages = i;
                await documents.UpdateAsync(doc, ct);
                return;
            }
            catch (InvalidOperationException)
            {
                doc.ProcessedPages = i + 1;
                await documents.UpdateAsync(doc, ct); // report progress even though this page failed
                continue;
            }

            if (!string.IsNullOrWhiteSpace(text))
                keptTexts.Add(text);

            doc.ProcessedPages = i + 1;
            await documents.UpdateAsync(doc, ct);
        }

        if (keptTexts.Count == 0)
        {
            doc.Status = DocumentProcessingStatus.Failed;
            doc.ProcessingError = "No readable text was found in the document.";
            await documents.UpdateAsync(doc, ct);
            return;
        }

        // Single kept page keeps the exact pre-multi-page behavior (no page header).
        doc.OcrText = keptTexts.Count == 1
            ? keptTexts[0]
            : string.Join("\n\n", keptTexts.Select((t, n) => $"--- Page {n + 1} ---\n\n{t}"));
        doc.SkippedPages = doc.PagePaths.Length - keptTexts.Count;
        doc.Status = DocumentProcessingStatus.Ready;
        await documents.UpdateAsync(doc, ct);
    }

    // Both a missing API key and an account-level OCR failure (quota/rate-limit/service outage,
    // see ClaudeOcrProvider.IsAccountLevelError) would fail every remaining page identically, so
    // either aborts the whole document with an accurate message instead of limping through every
    // page only to report the misleading "no readable text" once they've all been skipped.
    private static bool IsFatalError(InvalidOperationException ex) =>
        ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
}
