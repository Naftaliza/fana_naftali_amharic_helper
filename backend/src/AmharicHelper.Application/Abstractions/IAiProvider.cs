using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Abstractions;

/// <summary>
/// Abstraction over the LLM used for analysis and chat. The real MVP implementation is
/// <c>ClaudeAiProvider</c> (Anthropic); an <c>OpenAiProvider</c> stub exists behind the
/// same contract. Provider is chosen via the <c>Ai:Provider</c> config key.
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Analyze a document's extracted text and return the structured analysis shape:
    /// summary, documentType, urgencyLevel, keyPoints, requiredActions, deadlines,
    /// translatedAmharic, translatedSimpleHebrew.
    /// </summary>
    Task<DocumentAnalysisResult> AnalyzeAsync(
        string documentText,
        DocumentCategory category,
        CancellationToken ct = default);

    /// <summary>
    /// Answer a follow-up question grounded in the document text and prior chat history.
    /// </summary>
    Task<string> ChatAsync(
        string documentText,
        IReadOnlyList<ChatTurn> history,
        string question,
        Language responseLanguage,
        CancellationToken ct = default);
}

/// <summary>A prior chat turn passed as context to the AI.</summary>
public record ChatTurn(ChatRole Role, string Content);
