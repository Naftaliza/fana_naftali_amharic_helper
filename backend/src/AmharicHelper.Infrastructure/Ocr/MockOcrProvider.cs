using AmharicHelper.Application.Abstractions;

namespace AmharicHelper.Infrastructure.Ocr;

/// <summary>
/// No-op OCR provider for offline tests only (Ocr:Provider=Mock). It does NOT fabricate
/// document content — it returns an empty string. The real engine is <see cref="ClaudeOcrProvider"/>.
/// </summary>
public class MockOcrProvider : IOcrProvider
{
    public Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
        => Task.FromResult(string.Empty);
}
