namespace AmharicHelper.Application.Abstractions;

/// <summary>
/// Abstraction over OCR engines. The MVP ships a <c>MockOcrProvider</c>; Google Vision
/// and Azure OCR implementations are planned. Swap implementations via DI / config.
/// </summary>
public interface IOcrProvider
{
    /// <summary>Extract raw text from a document's bytes.</summary>
    /// <param name="fileBytes">The uploaded file content (PDF/JPG/PNG).</param>
    /// <param name="contentType">MIME type, e.g. <c>image/png</c> or <c>application/pdf</c>.</param>
    Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default);
}
