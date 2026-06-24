using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    bool HasAnalysis);

public record DocumentDetailDto(
    Guid Id,
    string FileName,
    string ContentType,
    string? OcrText,
    DateTime UploadedAt,
    DocumentAnalysisResult? Analysis);

public record ChatMessageDto(Guid Id, ChatRole Role, string Content, DateTime CreatedAt);

public record SendChatRequest(string Question, Language ResponseLanguage);

/// <summary>Body for anonymous trial speech: the analysis to read and the target language.</summary>
public record TrialSpeechRequest(DocumentAnalysisResult Analysis, Language Language);
