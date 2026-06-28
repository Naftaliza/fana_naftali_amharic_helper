using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

// ---- Upload + OCR ----
public record UploadDocumentCommand(
    Guid UserId, string FileName, string ContentType, byte[] Content)
    : IRequest<Result<DocumentSummaryDto>>;

public class UploadDocumentHandler(
    IFileStorage storage,
    IOcrProvider ocr,
    IDocumentRepository documents) : IRequestHandler<UploadDocumentCommand, Result<DocumentSummaryDto>>
{
    public async Task<Result<DocumentSummaryDto>> Handle(UploadDocumentCommand cmd, CancellationToken ct)
    {
        var path = await storage.SaveAsync(cmd.Content, cmd.FileName, ct);
        var ocrText = await ocr.ExtractTextAsync(cmd.Content, cmd.ContentType, ct);

        var doc = new Document
        {
            UserId = cmd.UserId,
            FileName = cmd.FileName,
            FilePath = path,
            ContentType = cmd.ContentType,
            OcrText = ocrText
        };
        await documents.AddAsync(doc, ct);

        return Result<DocumentSummaryDto>.Ok(
            new DocumentSummaryDto(doc.Id, doc.FileName, doc.ContentType, doc.UploadedAt, false));
    }
}

// ---- Delete ----
public record DeleteDocumentCommand(Guid UserId, Guid DocumentId) : IRequest<Result<bool>>;

public class DeleteDocumentHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses,
    IChatMessageRepository messages,
    IFileStorage storage) : IRequestHandler<DeleteDocumentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null || doc.UserId != cmd.UserId)
            return Result<bool>.Fail("Document not found.");

        // Remove children first to satisfy foreign keys, then the document and its file.
        await messages.DeleteByDocumentIdAsync(doc.Id, ct);
        await analyses.DeleteByDocumentIdAsync(doc.Id, ct);
        await documents.DeleteAsync(doc.Id, ct);
        await storage.DeleteAsync(doc.FilePath, ct);

        return Result<bool>.Ok(true);
    }
}

// ---- Analyze ----
public record AnalyzeDocumentCommand(Guid UserId, Guid DocumentId, DocumentCategory Category)
    : IRequest<Result<DocumentAnalysisResult>>;

public class AnalyzeDocumentHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses,
    ITtsAudioCacheRepository ttsCache,
    IAiProvider ai) : IRequestHandler<AnalyzeDocumentCommand, Result<DocumentAnalysisResult>>
{
    public async Task<Result<DocumentAnalysisResult>> Handle(AnalyzeDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null || doc.UserId != cmd.UserId)
            return Result<DocumentAnalysisResult>.Fail("Document not found.");
        if (string.IsNullOrWhiteSpace(doc.OcrText))
            return Result<DocumentAnalysisResult>.Fail("Document has no extracted text to analyze.");

        DocumentAnalysisResult result;
        try
        {
            result = await ai.AnalyzeAsync(doc.OcrText, cmd.Category, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Surface provider/config/parse errors clearly instead of a generic 500.
            return Result<DocumentAnalysisResult>.Fail(ex.Message);
        }

        // Re-analyzing replaces the previous result rather than stacking duplicates.
        // The spoken text changes too, so drop any cached audio for this document.
        await analyses.DeleteByDocumentIdAsync(doc.Id, ct);
        await ttsCache.DeleteByDocumentIdAsync(doc.Id, ct);
        await analyses.AddAsync(new DocumentAnalysis
        {
            DocumentId = doc.Id,
            Summary = result.Summary,
            DocumentType = result.DocumentType,
            Category = result.Category,
            UrgencyLevel = result.UrgencyLevel,
            KeyPoints = result.KeyPoints,
            RequiredActions = result.RequiredActions
                .Select(a => new RequiredAction(a.Description, a.IsMandatory)).ToList(),
            Deadlines = result.Deadlines
                .Select(d => new Deadline(d.Date, d.Description)).ToList(),
            Explanation = result.Explanation
        }, ct);

        return Result<DocumentAnalysisResult>.Ok(result);
    }
}
