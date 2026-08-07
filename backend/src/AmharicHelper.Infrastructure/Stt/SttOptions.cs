using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Infrastructure.Stt;

/// <summary>Bound from the <c>Stt</c> configuration section. AzureSpeechKey/AzureRegion default
/// from Tts's own config when left blank (see DependencyInjection.cs) — it's the same Azure
/// Speech resource, so no separate Railway env vars are required to turn this on.</summary>
public class SttOptions
{
    /// <summary>Only "Azure" is implemented.</summary>
    public string Provider { get; set; } = "Azure";

    public string AzureSpeechKey { get; set; } = string.Empty;
    // Deliberately blank, NOT a real default region like TtsOptions.AzureRegion has — the
    // DependencyInjection.cs PostConfigure fallback only inherits Tts's region when this is
    // blank. A non-empty default here would silently defeat that fallback (the blank-check would
    // never trigger) and send every request to the wrong region regardless of what Tts is
    // actually configured with.
    public string AzureRegion { get; set; } = string.Empty;

    /// <summary>Full override of the Fast Transcription endpoint URL. Leave blank to build the
    /// regional URL from AzureRegion; set this if your Speech resource instead requires the
    /// resource-name host (https://{resource}.cognitiveservices.azure.com/...).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "2024-11-15";

    /// <summary>Hard cap on the uploaded recording — also enforced client-side and via
    /// [RequestSizeLimit] on the controller action; this is the last line of defense.</summary>
    public long MaxAudioBytes { get; set; } = 8_000_000;

    /// <summary>Locales sent to Fast Transcription's language-identification. Users code-switch
    /// between Amharic/Hebrew and English/Hebrew mid-sentence (Hebrew institution names inside
    /// an Amharic question), so Hebrew is always included alongside the UI language rather than
    /// sending a single locale.</summary>
    public string[] LocalesFor(Language language) => language switch
    {
        Language.Amharic => ["am-ET", "he-IL"],
        Language.English => ["en-US", "he-IL"],
        _ => ["he-IL"],
    };
}
