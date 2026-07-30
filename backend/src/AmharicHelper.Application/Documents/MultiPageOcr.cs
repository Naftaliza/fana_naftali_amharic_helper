using AmharicHelper.Application.Abstractions;

namespace AmharicHelper.Application.Documents;

/// <summary>
/// Runs OCR over an ordered set of pages and concatenates the results into the single text the rest
/// of the pipeline (analysis, TTS, chat) consumes. Shared by the logged-in upload flow and the
/// anonymous trial flow so both behave identically.
///
/// Failure policy (see plan):
/// - A page "succeeds" only if OCR returns non-whitespace text. Claude returns an empty string for a
///   blank/unreadable page (it does not throw), so empty == skipped.
/// - A page-level OCR exception (e.g. a transient API failure that survived retries) skips that page
///   but the batch continues.
/// - A configuration/auth error (missing API key) or an account-level OCR failure (quota exceeded,
///   rate limited, service outage) would fail every page identically, so it is rethrown to fail
///   the whole upload with an accurate message rather than silently producing an empty document.
/// - If no page yields text, the caller treats it as a failure (nothing is saved).
/// </summary>
public static class MultiPageOcr
{
    public record Result(IReadOnlyList<int> KeptPageIndices, string CombinedText, int SkippedCount);

    public static async Task<Result> RunAsync(
        IReadOnlyList<(byte[] Content, string ContentType)> pages,
        IOcrProvider ocr,
        CancellationToken ct)
    {
        var keptIndices = new List<int>();
        var keptTexts = new List<string>();

        // Sequential on purpose: the Anthropic client only retries a few times on 429, so firing
        // every page in parallel would collide with rate limits.
        for (var i = 0; i < pages.Count; i++)
        {
            string text;
            try
            {
                text = await ocr.ExtractTextAsync(pages[i].Content, pages[i].ContentType, ct);
            }
            catch (InvalidOperationException ex) when (IsFatalError(ex))
            {
                throw; // doomed for every page — fail the whole batch
            }
            catch (InvalidOperationException)
            {
                continue; // this page failed; skip it and keep going
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                keptIndices.Add(i);
                keptTexts.Add(text);
            }
        }

        var combined = keptTexts.Count switch
        {
            0 => string.Empty,
            // Single page keeps the exact pre-multi-page behavior (no page header).
            1 => keptTexts[0],
            _ => string.Join("\n\n", keptTexts.Select((t, n) => $"--- Page {n + 1} ---\n\n{t}"))
        };

        return new Result(keptIndices, combined, pages.Count - keptIndices.Count);
    }

    // See DocumentProcessor.IsFatalError — same reasoning, duplicated because this file has no
    // dependency on that one.
    private static bool IsFatalError(InvalidOperationException ex) =>
        ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
}
