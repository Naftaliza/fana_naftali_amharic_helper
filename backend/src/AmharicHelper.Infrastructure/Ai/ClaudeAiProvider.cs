using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Prompts;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// AI provider backed by the Anthropic Messages API. Performs real analysis and chat;
/// requires an Anthropic API key (no mock fallback).
/// </summary>
public class ClaudeAiProvider(
    HttpClient http,
    IOptions<AiOptions> options,
    ILogger<ClaudeAiProvider> logger) : IAiProvider
{
    private readonly AiOptions _opts = options.Value;
    // Web defaults + accept string enum values (e.g. urgencyLevel: "Medium"), tolerate whatever
    // date format the model emits for deadlines, and tolerate an array field (requiredActions,
    // keyPoints, deadlines) coming back double-encoded as a JSON string instead of a real array.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(), new TolerantDateTimeConverter(), new TolerantListConverterFactory() }
    };

    // How many times to regenerate the whole analysis if the model's JSON comes back
    // unparseable. Investigated live: for a heavily-nested, trilingual schema, Claude
    // occasionally double-encodes an array field (e.g. requiredActions) as a JSON string, and
    // rarely garbles the escaping while doing so badly enough that no amount of tolerant
    // parsing can recover it (a genuinely corrupted byte sequence, not just an odd shape). A
    // fresh generation reliably avoids the same slip — this is the same rationale as retrying
    // a transient HTTP failure, just at the semantic level of "the output was bad" rather than
    // "the request failed".
    private const int MaxAnalyzeAttempts = 3;

    public async Task<DocumentAnalysisResult> AnalyzeAsync(
        string documentText, DocumentCategory category, CancellationToken ct = default)
    {
        RequireKey();

        var system = PromptTemplates.BuildAnalysisPrompt(category);
        JsonException? lastError = null;
        for (var attempt = 1; attempt <= MaxAnalyzeAttempts; attempt++)
        {
            // Use a forced tool call so the model returns the result as a structured JSON
            // object (the tool's `input`) rather than free-typed text. This avoids parse
            // failures when content contains characters that collide with JSON syntax —
            // e.g. Hebrew abbreviations like בע"מ / עו"ד that use a straight double-quote.
            var json = await CallToolAsync(system, $"DOCUMENT TEXT:\n{documentText}", ct);
            try
            {
                var result = JsonSerializer.Deserialize<DocumentAnalysisResult>(json, JsonOpts);
                if (result is null)
                    throw new InvalidOperationException("The AI returned an empty analysis.");
                return result;
            }
            catch (JsonException ex)
            {
                lastError = ex;
                // Deliberately not logging `json` — it's the model's structured translation of
                // the uploaded document (bank/government/healthcare letters), i.e. PII, and it
                // doesn't belong in plaintext logs. Length is enough to distinguish "empty" from
                // "truncated".
                logger.LogWarning(ex,
                    "Failed to parse Claude analysis JSON ({Length} chars) on attempt {Attempt}/{Max}.",
                    json.Length, attempt, MaxAnalyzeAttempts);
            }
        }

        logger.LogError(lastError, "Failed to parse Claude analysis JSON after {Max} attempts.", MaxAnalyzeAttempts);
        throw new InvalidOperationException("The AI returned an unparseable analysis. Please try again.");
    }

    // A textual field localized to all three languages (mirrors LocalizedText).
    private static object LocalizedSchema() => new
    {
        type = "object",
        properties = new
        {
            he = new { type = "string" },
            am = new { type = "string" },
            en = new { type = "string" }
        },
        required = new[] { "he", "am", "en" }
    };

    // JSON schema for the analysis tool, mirroring DocumentAnalysisResult.
    private static readonly object AnalysisSchema = new
    {
        type = "object",
        properties = new
        {
            summary = LocalizedSchema(),
            documentType = LocalizedSchema(),
            category = new { type = "string", @enum = new[] { "Government", "Bank", "Insurance", "Employment", "Healthcare", "Municipality", "Other" } },
            urgencyLevel = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Critical" } },
            keyPoints = new { type = "array", items = LocalizedSchema() },
            requiredActions = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        description = LocalizedSchema(),
                        isMandatory = new { type = "boolean" }
                    },
                    required = new[] { "description", "isMandatory" }
                }
            },
            deadlines = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        date = new { type = new[] { "string", "null" } },
                        description = LocalizedSchema()
                    },
                    required = new[] { "description" }
                }
            },
            explanation = LocalizedSchema()
        },
        required = new[]
        {
            "summary", "documentType", "category", "urgencyLevel", "keyPoints",
            "requiredActions", "deadlines", "explanation"
        }
    };

    public async Task<string> ChatAsync(
        string documentText, IReadOnlyList<ChatTurn> history, string question,
        Language responseLanguage, CancellationToken ct = default)
    {
        RequireKey();

        var system = PromptTemplates.BuildChatPrompt(responseLanguage);
        var convo = string.Join("\n", history.Select(t => $"{t.Role}: {t.Content}"));
        var user = $"DOCUMENT TEXT:\n{documentText}\n\nCONVERSATION SO FAR:\n{convo}\n\nQUESTION:\n{question}";
        return await CallAsync(system, user, ct);
    }

    private void RequireKey()
    {
        if (string.IsNullOrWhiteSpace(_opts.AnthropicApiKey))
            throw new InvalidOperationException("Anthropic API key is not configured (Ai:AnthropicApiKey).");
    }

    private async Task<string> CallAsync(string system, string user, CancellationToken ct)
    {
        var request = new
        {
            model = _opts.AnthropicModel,
            // Generous cap: the analysis includes full Amharic + Hebrew translations, which
            // can be long. Too low a limit truncates the JSON and makes it unparseable.
            max_tokens = 8192,
            system = CachedSystemBlocks(system),
            messages = new[] { new { role = "user", content = user } }
        };

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, () =>
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
            logger.LogError("Anthropic API failed ({Status}): {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"AI request failed: {(int)resp.StatusCode} {resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        AnthropicResponseHelpers.LogUsage(doc.RootElement, logger, "chat");
        AnthropicResponseHelpers.ThrowIfTruncated(doc.RootElement, logger, "chat");
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    // Wraps a system prompt as a single cached content block. BuildAnalysisPrompt's output is
    // static per category (no per-user/per-document interpolation), so this isn't just a
    // same-request optimization — every user analyzing e.g. a Bank document within the cache's
    // TTL shares the same cache entry. Below Anthropic's ~1024-token minimum (the chat prompt is
    // shorter than that) the marker is simply ignored, not harmful.
    private static object[] CachedSystemBlocks(string system) =>
        [new { type = "text", text = system, cache_control = new { type = "ephemeral" } }];

    /// <summary>
    /// Calls Anthropic with a single forced tool and returns the tool's `input` as raw JSON.
    /// Because the model fills a JSON object via the tool-call mechanism, the result is
    /// always well-formed JSON (no escaping pitfalls from free-text output).
    /// </summary>
    private async Task<string> CallToolAsync(string system, string user, CancellationToken ct)
    {
        var request = new
        {
            model = _opts.AnthropicModel,
            max_tokens = 8192,
            system = CachedSystemBlocks(system),
            tools = new[]
            {
                new
                {
                    name = "submit_analysis",
                    description = "Return the structured analysis of the document.",
                    input_schema = AnalysisSchema
                }
            },
            tool_choice = new { type = "tool", name = "submit_analysis" },
            messages = new[] { new { role = "user", content = user } }
        };

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, () =>
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
            logger.LogError("Anthropic API failed ({Status}): {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"AI request failed: {(int)resp.StatusCode} {resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        AnthropicResponseHelpers.LogUsage(doc.RootElement, logger, "analyze");
        AnthropicResponseHelpers.ThrowIfTruncated(doc.RootElement, logger, "analyze");
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "tool_use"
                && block.TryGetProperty("input", out var input))
            {
                return input.GetRawText();
            }
        }
        throw new InvalidOperationException("The AI did not return a structured analysis.");
    }
}
