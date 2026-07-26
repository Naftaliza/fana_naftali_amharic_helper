using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

public record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    bool HasAnalysis,
    DocumentProcessingStatus Status);

/// <summary>
/// Result of queuing a (possibly multi-page) upload for background OCR. The pages haven't been
/// transcribed yet at this point — Status starts Pending and the client polls
/// GET /documents/{id} for progress (ProcessedPages/TotalPages) and completion.
/// </summary>
public record UploadDocumentResultDto(
    Guid Id,
    string FileName,
    string ContentType,
    DateTime UploadedAt,
    DocumentProcessingStatus Status,
    int TotalPages);

/// <summary>One uploaded page: its original bytes and metadata, before OCR.</summary>
public record UploadPage(string FileName, string ContentType, byte[] Content);

public record DocumentDetailDto(
    Guid Id,
    string FileName,
    string ContentType,
    string? OcrText,
    DateTime UploadedAt,
    DocumentAnalysisResult? Analysis,
    DocumentProcessingStatus Status,
    int ProcessedPages,
    int TotalPages,
    int SkippedPages,
    string? ProcessingError);

public record ChatMessageDto(Guid Id, ChatRole Role, string Content, DateTime CreatedAt);

public record SendChatRequest(string Question, Language ResponseLanguage);

/// <summary>Body for anonymous trial speech: the analysis to read and the target language.</summary>
public record TrialSpeechRequest(DocumentAnalysisResult Analysis, Language Language);
