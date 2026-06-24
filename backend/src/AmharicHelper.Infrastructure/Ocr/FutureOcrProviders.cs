using AmharicHelper.Application.Abstractions;

namespace AmharicHelper.Infrastructure.Ocr;

/// <summary>
/// Planned Google Cloud Vision OCR implementation. Wire up Google.Cloud.Vision.V1 here and
/// register in DI in place of <see cref="MockOcrProvider"/> via the <c>Ocr:Provider</c> config key.
/// </summary>
public class GoogleVisionOcrProvider : IOcrProvider
{
    public Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
        => throw new NotImplementedException("Google Vision OCR is planned for a future release.");
}

/// <summary>
/// Planned Azure AI Vision (Read API) OCR implementation. Wire up Azure.AI.Vision here.
/// </summary>
public class AzureOcrProvider : IOcrProvider
{
    public Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
        => throw new NotImplementedException("Azure OCR is planned for a future release.");
}
