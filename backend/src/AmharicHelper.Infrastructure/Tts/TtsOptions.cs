using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Infrastructure.Tts;

/// <summary>Bound from the <c>Tts</c> configuration section.</summary>
public class TtsOptions
{
    /// <summary>"Azure" (default, has native Amharic voices) or "ElevenLabs".</summary>
    public string Provider { get; set; } = "Azure";

    // --- Azure AI Speech ---
    public string AzureSpeechKey { get; set; } = string.Empty;

    /// <summary>Azure region of the Speech resource, e.g. "eastus", "westeurope".</summary>
    public string AzureRegion { get; set; } = "eastus";

    /// <summary>Per-language Azure neural voice names. Amharic is the headline reason for Azure.</summary>
    public string AmharicVoice { get; set; } = "am-ET-MekdesNeural";   // female; am-ET-AmehaNeural = male
    public string HebrewVoice { get; set; } = "he-IL-HilaNeural";      // female; he-IL-AvriNeural = male
    public string EnglishVoice { get; set; } = "en-US-JennyNeural";

    // --- ElevenLabs (kept as a selectable fallback; no native Amharic) ---
    public string ElevenLabsApiKey { get; set; } = string.Empty;
    public string ElevenLabsVoiceId { get; set; } = "21m00Tcm4TlvDq8ikWAM";
    public string ElevenLabsModelId { get; set; } = "eleven_multilingual_v2";

    /// <summary>Resolve the Azure voice + BCP-47 locale for a language.</summary>
    public (string Voice, string Locale) AzureVoiceFor(Language language) => language switch
    {
        Language.Amharic => (AmharicVoice, "am-ET"),
        Language.English => (EnglishVoice, "en-US"),
        _ => (HebrewVoice, "he-IL"),
    };
}
