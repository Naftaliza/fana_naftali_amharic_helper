using AmharicHelper.Application.DTOs;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Shared validation + buffering for (multi-page) document uploads. Used by both the logged-in and
/// the anonymous-trial controllers so the rules stay identical. Validates here rather than in a
/// MediatR pipeline because the app has no validation behavior wired up.
/// </summary>
internal static class UploadValidation
{
    // The server must not trust the client's declared type blindly — the OCR provider silently
    // defaults unknown image types to JPEG, so reject anything outside this set up front.
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "application/pdf"
    };

    /// <summary>
    /// Validates the uploaded files and reads them into <see cref="UploadPage"/>s. Returns an error
    /// message (and null pages) on any problem; otherwise the ordered, non-empty pages.
    /// </summary>
    public static async Task<(List<UploadPage>? Pages, string? Error)> BuildPagesAsync(
        IReadOnlyList<IFormFile>? files, int maxPages, CancellationToken ct)
    {
        var nonEmpty = (files ?? []).Where(f => f.Length > 0).ToList();
        if (nonEmpty.Count == 0)
            return (null, "No file provided.");
        if (nonEmpty.Count > maxPages)
            return (null, $"Too many pages — up to {maxPages} allowed.");

        foreach (var f in nonEmpty)
        {
            if (!AllowedContentTypes.Contains(f.ContentType))
                return (null, $"Unsupported file type: {f.ContentType}. Use JPG, PNG, or PDF.");
        }

        var pages = new List<UploadPage>(nonEmpty.Count);
        foreach (var f in nonEmpty)
        {
            using var ms = new MemoryStream();
            await f.CopyToAsync(ms, ct);
            pages.Add(new UploadPage(f.FileName, f.ContentType, ms.ToArray()));
        }
        return (pages, null);
    }
}
