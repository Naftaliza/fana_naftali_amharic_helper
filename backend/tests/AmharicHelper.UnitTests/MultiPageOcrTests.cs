using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Documents;
using Xunit;

namespace AmharicHelper.UnitTests;

public class MultiPageOcrTests
{
    // Map: page marker byte -> result. 1=text, 0=blank(empty), 2=throw transient,
    // 3=throw config, 4=throw account-level service error (quota/rate-limit/outage).
    private sealed class FakeOcr : IOcrProvider
    {
        public List<byte[]> Seen { get; } = new();

        public Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
        {
            Seen.Add(fileBytes);
            return fileBytes[0] switch
            {
                1 => Task.FromResult($"text-{fileBytes[1]}"),
                0 => Task.FromResult(""),
                2 => throw new InvalidOperationException("OCR failed: 500 InternalServerError"),
                3 => throw new InvalidOperationException("Anthropic API key is not configured (Ai:AnthropicApiKey)."),
                4 => throw new InvalidOperationException("OCR service is temporarily unavailable (429)."),
                _ => Task.FromResult("?"),
            };
        }
    }

    private static (byte[] Content, string ContentType) Page(byte marker, byte index) =>
        (new byte[] { marker, index }, "image/jpeg");

    [Fact]
    public async Task Blank_pages_are_skipped_and_remaining_pages_renumbered()
    {
        var ocr = new FakeOcr();

        var result = await MultiPageOcr.RunAsync(new[] { Page(1, 0), Page(0, 1), Page(1, 2) }, ocr, default);

        Assert.Equal(new[] { 0, 2 }, result.KeptPageIndices);
        Assert.Equal("--- Page 1 ---\n\ntext-0\n\n--- Page 2 ---\n\ntext-2", result.CombinedText);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task Transient_page_error_is_skipped_and_the_batch_continues()
    {
        var ocr = new FakeOcr();

        var result = await MultiPageOcr.RunAsync(new[] { Page(2, 0), Page(1, 1) }, ocr, default);

        Assert.Equal("text-1", result.CombinedText);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task Config_error_is_rethrown_and_stops_the_batch()
    {
        var ocr = new FakeOcr();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MultiPageOcr.RunAsync(new[] { Page(3, 0), Page(1, 1) }, ocr, default));

        Assert.Contains("API key", ex.Message);
        Assert.Single(ocr.Seen); // second page never attempted
    }

    [Fact]
    public async Task Account_level_service_error_is_rethrown_with_an_accurate_message_and_stops_the_batch()
    {
        var ocr = new FakeOcr();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MultiPageOcr.RunAsync(new[] { Page(4, 0), Page(1, 1) }, ocr, default));

        Assert.Contains("temporarily unavailable", ex.Message);
        Assert.Single(ocr.Seen); // second page never attempted
    }
}
