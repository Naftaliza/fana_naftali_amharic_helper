using System.Security;
using System.Text;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AmharicHelper.Infrastructure.Tts;

/// <summary>
/// Text-to-speech via the Azure AI Speech REST API. Azure is the only major provider
/// with native Amharic neural voices (am-ET-MekdesNeural / am-ET-AmehaNeural).
/// Returns MP3 audio. Requires <c>Tts:AzureSpeechKey</c> and <c>Tts:AzureRegion</c>.
/// </summary>
public class AzureTtsProvider(
    HttpClient http,
    IOptions<TtsOptions> options,
    ILogger<AzureTtsProvider> logger) : ITtsProvider
{
    private readonly TtsOptions _opts = options.Value;

    public async Task<TtsAudio> SynthesizeAsync(string text, Language language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.AzureSpeechKey))
            throw new InvalidOperationException("Azure Speech key is not configured (Tts:AzureSpeechKey).");

        var (voice, locale) = _opts.AzureVoiceFor(language);
        var url = $"https://{_opts.AzureRegion}.tts.speech.microsoft.com/cognitiveservices/v1";

        // Azure TTS takes SSML. Escape the text so it can't break the markup.
        var ssml =
            $"<speak version='1.0' xml:lang='{locale}'>" +
            $"<voice xml:lang='{locale}' name='{voice}'>{SecurityElement.Escape(text)}</voice></speak>";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml")
        };
        req.Headers.Add("Ocp-Apim-Subscription-Key", _opts.AzureSpeechKey);
        req.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        req.Headers.Add("User-Agent", "AmharicHelper");

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            logger.LogError("Azure TTS failed ({Status}): {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"Azure TTS failed: {(int)resp.StatusCode} {resp.StatusCode}");
        }

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return new TtsAudio(bytes, "audio/mpeg");
    }
}
