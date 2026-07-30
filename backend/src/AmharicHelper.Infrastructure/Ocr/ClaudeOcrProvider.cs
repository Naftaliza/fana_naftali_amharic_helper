using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Infrastructure.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AmharicHelper.Infrastructure.Ocr;

/// <summary>
/// OCR via Claude's vision capability. Sends the uploaded image or PDF to the Anthropic
/// Messages API and asks the model to transcribe the text verbatim. Reads Hebrew, Amharic
/// and Latin scripts. Uses the same Anthropic key/model as <see cref="Ai.ClaudeAiProvider"/>.
/// </summary>
public class ClaudeOcrProvider(
    HttpClient http,
    IOptions<AiOptions> options,
    ILogger<ClaudeOcrProvider> logger) : IOcrProvider
{
    private readonly AiOptions _opts = options.Value;

    private const string Instruction =
        "Transcribe ALL text in this document exactly as it appears, preserving line breaks " +
        "and the original language/script. Do not translate, summarize, or add commentary. " +
        "Output only the raw transcribed text.";

    public async Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.AnthropicApiKey))
            throw new InvalidOperationException("Anthropic API key is not configured (Ai:AnthropicApiKey).");

        var base64 = Convert.ToBase64String(fileBytes);

        // PDFs use a "document" content block; images use an "image" block.
        object mediaBlock = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            ? new { type = "document", source = new { type = "base64", media_type = "application/pdf", data = base64 } }
            : new { type = "image", source = new { type = "base64", media_type = NormalizeImageType(contentType), data = base64 } };

        var request = new
        {
            model = _opts.AnthropicOcrModel,
            max_tokens = 4096,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[] { mediaBlock, new { type = "text", text = Instruction } }
                }
            }
        };

        using var resp = await Ai.AnthropicHttp.SendWithRetryAsync(http, () =>
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(request)
            };
            msg.Headers.Add("x-api-key", _opts.AnthropicApiKey);
            msg.Headers.Add("anthropic-version", "2023-06-01");
            return msg;
        }, logger, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            logger.LogError("Claude OCR failed ({Status}): {Body}", resp.StatusCode, err);
            if (IsAccountLevelError(resp.StatusCode, err))
                throw new InvalidOperationException($"OCR service is temporarily unavailable ({(int)resp.StatusCode}).");
            throw new InvalidOperationException($"OCR failed: {(int)resp.StatusCode} {resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        Ai.AnthropicResponseHelpers.LogUsage(doc.RootElement, logger, "ocr");
        Ai.AnthropicResponseHelpers.ThrowIfTruncated(doc.RootElement, logger, "ocr");
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private static string NormalizeImageType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpg" or "image/jpeg" => "image/jpeg",
        "image/png" => "image/png",
        "image/gif" => "image/gif",
        "image/webp" => "image/webp",
        _ => "image/jpeg"
    };

    // Distinguishes "the account/service can't OCR anything right now" (quota exhausted, rate
    // limited, unauthorized, or the API itself is down) from "this specific request was rejected"
    // (e.g. a malformed image). The former would fail identically for every page and every future
    // document, so callers treat it as a hard failure instead of skipping just this page.
    private static bool IsAccountLevelError(HttpStatusCode status, string body) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
        || (int)status >= 500
        || body.Contains("usage limit", StringComparison.OrdinalIgnoreCase)
        || body.Contains("rate_limit_error", StringComparison.OrdinalIgnoreCase)
        || body.Contains("overloaded_error", StringComparison.OrdinalIgnoreCase);
}
