using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Abstractions;

/// <summary>The result of transcribing a short voice recording.</summary>
public record SttResult(string Text, string? Locale);

/// <summary>
/// Abstraction over speech-to-text engines, mirroring ITtsProvider's shape. The Azure
/// implementation backs the chat "ask out loud" mic button.
/// </summary>
public interface ISttProvider
{
    /// <summary>Transcribe an audio recording, biased toward the requested UI language (and
    /// Hebrew, since users code-switch between Amharic/Hebrew mid-sentence).</summary>
    Task<SttResult> TranscribeAsync(
        Stream audio, string fileName, string contentType, Language language, CancellationToken ct = default);
}
