using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    bool HasAnalysis);

/// <summary>
/// Result of a (possibly multi-page) upload. <paramref name="PageCount"/> is the number of pages
/// whose text was kept; <paramref name="SkippedPages"/> is how many pages were dropped because they
/// were blank/unreadable, so the UI can warn the user (e.g. "page 4 couldn't be read").
/// </summary>
public record UploadDocumentResultDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    int PageCount,
    int SkippedPages);

/// <summary>One uploaded page: its original bytes and metadata, before OCR.</summary>
public record UploadPage(string FileName, string ContentType, byte[] Content);

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
