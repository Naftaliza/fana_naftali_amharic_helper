using System.Net.Http.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AmharicHelper.Infrastructure.Tts;

/// <summary>
/// Text-to-speech via the ElevenLabs API. Returns MP3 audio bytes.
/// Requires <c>Tts:ElevenLabsApiKey</c> (or env <c>ELEVENLABS_API_KEY</c>).
/// </summary>
public class ElevenLabsTtsProvider(
    HttpClient http,
    IOptions<TtsOptions> options,
    ILogger<ElevenLabsTtsProvider> logger) : ITtsProvider
{
    private readonly TtsOptions _opts = options.Value;

    public async Task<TtsAudio> SynthesizeAsync(string text, Language language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ElevenLabsApiKey))
            throw new InvalidOperationException("ElevenLabs API key is not configured (Tts:ElevenLabsApiKey).");

        var url = $"https://api.elevenlabs.io/v1/text-to-speech/{_opts.ElevenLabsVoiceId}";
        var body = new
        {
            text,
            model_id = _opts.ElevenLabsModelId,
            voice_settings = new { stability = 0.5, similarity_boost = 0.75 }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        req.Headers.Add("xi-api-key", _opts.ElevenLabsApiKey);
        req.Headers.Add("Accept", "audio/mpeg");

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            logger.LogError("ElevenLabs TTS failed ({Status}): {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"ElevenLabs TTS failed: {resp.StatusCode}");
        }

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return new TtsAudio(bytes, "audio/mpeg");
    }
}
