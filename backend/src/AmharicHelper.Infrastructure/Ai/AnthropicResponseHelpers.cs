using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// Shared helpers for inspecting an Anthropic Messages API response body. Neither of these existed
/// before: token usage was never read (no visibility into spend or prompt-cache effectiveness),
/// and stop_reason was never checked (a max_tokens cutoff silently produced truncated OCR text or
/// an incomplete tool-call analysis with no indication of the real cause).
/// </summary>
public static class AnthropicResponseHelpers
{
    /// <summary>Logs input/output/cache token counts at Information level — the only spend
    /// visibility this app has today; see the plan for a persisted per-user/org ledger.</summary>
    public static void LogUsage(JsonElement root, ILogger logger, string callLabel)
    {
        if (!root.TryGetProperty("usage", out var usage)) return;
        var input = usage.TryGetProperty("input_tokens", out var i) ? i.GetInt32() : 0;
        var output = usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
        var cacheRead = usage.TryGetProperty("cache_read_input_tokens", out var cr) ? cr.GetInt32() : 0;
        var cacheWrite = usage.TryGetProperty("cache_creation_input_tokens", out var cw) ? cw.GetInt32() : 0;
        logger.LogInformation(
            "Anthropic usage [{Call}]: input={Input} output={Output} cacheRead={CacheRead} cacheWrite={CacheWrite}",
            callLabel, input, output, cacheRead, cacheWrite);
    }

    /// <summary>Throws if max_tokens cut the response off mid-content — continuing silently would
    /// mean handing truncated OCR text (or a truncated tool-call analysis) to the rest of the
    /// pipeline as if it were complete.</summary>
    public static void ThrowIfTruncated(JsonElement root, ILogger logger, string callLabel)
    {
        if (root.TryGetProperty("stop_reason", out var sr) && sr.GetString() == "max_tokens")
        {
            logger.LogError("Anthropic response truncated by max_tokens [{Call}]", callLabel);
            throw new InvalidOperationException(
                "The AI response was cut off because it was too long. Please try again.");
        }
    }
}
