using System.Net.Http.Headers;
using System.Text.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AmharicHelper.Infrastructure.Stt;

/// <summary>
/// Speech-to-text via Azure AI Speech's Fast Transcription REST API — chosen over the older
/// short-audio REST API because Fast Transcription accepts WebM/Opus and AAC directly (exactly
/// what MediaRecorder produces on Android Chrome and iOS Safari respectively), while short-audio
/// only accepts WAV-PCM or OGG-Opus and would need server-side transcoding (a new native
/// dependency in the Railway image). Requires Stt:AzureSpeechKey/AzureRegion, which default from
/// Tts's own config when unset (see DependencyInjection.cs) — it's the same Azure Speech
/// resource used for TTS.
/// </summary>
public class AzureSttProvider(
    HttpClient http,
    IOptions<SttOptions> options,
    ILogger<AzureSttProvider> logger) : ISttProvider
{
    private readonly SttOptions _opts = options.Value;

    public async Task<SttResult> TranscribeAsync(
        Stream audio, string fileName, string contentType, Language language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.AzureSpeechKey))
            throw new InvalidOperationException("Azure Speech key is not configured (Stt:AzureSpeechKey).");

        var url = string.IsNullOrWhiteSpace(_opts.Endpoint)
            ? $"https://{_opts.AzureRegion}.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version={_opts.ApiVersion}"
            : _opts.Endpoint;

        using var content = new MultipartFormDataContent();
        using var audioContent = new StreamContent(audio);
        // MediaTypeHeaderValue's single-string constructor does strict RFC 7231 parsing and
        // throws FormatException on the exact Content-Type MediaRecorder sends browser-side
        // (e.g. "audio/webm;codecs=opus" — no space before the parameter). Azure's endpoint only
        // needs the container format to pick a decoder, not the codec parameter, so strip to the
        // base type/subtype rather than trying to satisfy .NET's stricter parser.
        var baseContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Split(';')[0].Trim();
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(baseContentType);
        content.Add(audioContent, "audio", string.IsNullOrWhiteSpace(fileName) ? "question.webm" : fileName);

        // The "definition" part is a JSON *string* field, not nested multipart JSON — Fast
        // Transcription's documented shape.
        var definition = JsonSerializer.Serialize(new { locales = _opts.LocalesFor(language) });
        content.Add(new StringContent(definition), "definition");

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        req.Headers.Add("Ocp-Apim-Subscription-Key", _opts.AzureSpeechKey);
        req.Headers.Add("User-Agent", "Fana");

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            logger.LogError("Azure fast transcription failed ({Status}): {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"Azure transcription failed: {(int)resp.StatusCode} {resp.StatusCode}");
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var text = "";
        if (root.TryGetProperty("combinedPhrases", out var combined) && combined.ValueKind == JsonValueKind.Array)
        {
            var first = combined.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("text", out var t))
                text = t.GetString() ?? "";
        }

        string? locale = null;
        if (root.TryGetProperty("phrases", out var phrases) && phrases.ValueKind == JsonValueKind.Array)
        {
            var first = phrases.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("locale", out var l))
                locale = l.GetString();
        }

        return new SttResult(text.Trim(), locale);
    }
}
