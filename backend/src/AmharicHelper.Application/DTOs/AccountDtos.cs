using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

/// <summary>A full export of one user's account (GDPR Art. 15 "right of access") — their profile
/// plus every document's OCR text, analysis, and chat history.</summary>
public record AccountExportDto(
    Guid UserId,
    string Email,
    string DisplayName,
    DateTime GeneratedAt,
    IReadOnlyList<ExportedDocumentDto> Documents);

public record ExportedDocumentDto(
    Guid Id,
    string FileName,
    DateTime UploadedAt,
    string? OcrText,
    DocumentAnalysisResult? Analysis,
    IReadOnlyList<ExportedChatMessageDto> ChatMessages);

public record ExportedChatMessageDto(ChatRole Role, string Content, DateTime CreatedAt);
