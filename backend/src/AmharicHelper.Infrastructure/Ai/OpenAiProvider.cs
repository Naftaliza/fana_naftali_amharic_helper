using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// Planned OpenAI implementation behind <see cref="IAiProvider"/>. Selectable via the
/// <c>Ai:Provider=OpenAI</c> config key. Implement using the Chat Completions / Responses API
/// with JSON mode, mirroring <see cref="ClaudeAiProvider"/>'s prompt + parse flow.
/// </summary>
public class OpenAiProvider : IAiProvider
{
    public Task<DocumentAnalysisResult> AnalyzeAsync(
        string documentText, DocumentCategory category, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is planned. Use Ai:Provider=Claude.");

    public Task<string> ChatAsync(
        string documentText, IReadOnlyList<ChatTurn> history, string question,
        Language responseLanguage, CancellationToken ct = default)
        => throw new NotImplementedException("OpenAI provider is planned. Use Ai:Provider=Claude.");
}
