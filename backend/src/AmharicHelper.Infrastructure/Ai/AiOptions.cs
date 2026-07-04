namespace AmharicHelper.Infrastructure.Ai;

/// <summary>Bound from the <c>Ai</c> configuration section.</summary>
public class AiOptions
{
    /// <summary>"Claude" (default) or "OpenAI".</summary>
    public string Provider { get; set; } = "Claude";
    public string? AnthropicApiKey { get; set; }
    public string AnthropicModel { get; set; } = "claude-sonnet-5";
    public string? OpenAiApiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o";
}
