using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Abstractions;

/// <summary>The audio produced by a text-to-speech provider.</summary>
public record TtsAudio(byte[] Content, string ContentType);

/// <summary>
/// Abstraction over text-to-speech engines. The MVP implementation is
/// <c>ElevenLabsTtsProvider</c>. Selected via the <c>Tts:Provider</c> config key.
/// </summary>
public interface ITtsProvider
{
    /// <summary>Synthesize speech for the given text in the requested language.</summary>
    Task<TtsAudio> SynthesizeAsync(string text, Language language, CancellationToken ct = default);
}
