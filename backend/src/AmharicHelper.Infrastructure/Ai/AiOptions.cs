namespace AmharicHelper.Infrastructure.Ai;

/// <summary>Bound from the <c>Ai</c> configuration section.</summary>
public class AiOptions
{
    /// <summary>"Claude" (default) or "OpenAI".</summary>
    public string Provider { get; set; } = "Claude";
    public string? AnthropicApiKey { get; set; }
    public string AnthropicModel { get; set; } = "claude-sonnet-5";

    /// <summary>Model used for OCR (page transcription) — deliberately a cheaper/faster tier than
    /// <see cref="AnthropicModel"/>. OCR is pure transcription, not analysis-grade reasoning, and
    /// it's the highest-volume call (up to 10 per multi-page upload), so this is where model choice
    /// has the most cost leverage.</summary>
    public string AnthropicOcrModel { get; set; } = "claude-haiku-4-5-20251001";

    public string? OpenAiApiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o";
}
