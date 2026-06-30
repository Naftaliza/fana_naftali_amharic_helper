using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.Documents;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

// ---- Upload + OCR (one or more pages) ----
public record UploadDocumentCommand(Guid UserId, IReadOnlyList<UploadPage> Pages)
    : IRequest<Result<UploadDocumentResultDto>>;

public class UploadDocumentHandler(
    IFileStorage storage,
    IOcrProvider ocr,
    IDocumentRepository documents) : IRequestHandler<UploadDocumentCommand, Result<UploadDocumentResultDto>>
{
    public async Task<Result<UploadDocumentResultDto>> Handle(UploadDocumentCommand cmd, CancellationToken ct)
    {
        if (cmd.Pages.Count == 0)
            return Result<UploadDocumentResultDto>.Fail("No pages provided.");

        // OCR every page first, then save only the kept ones — so a mid-batch OCR failure leaves no
        // orphaned files on disk. A configuration error (missing key) throws out to the controller.
        MultiPageOcr.Result ocrResult;
        try
        {
            ocrResult = await MultiPageOcr.RunAsync(
                cmd.Pages.Select(p => (p.Content, p.ContentType)).ToList(), ocr, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result<UploadDocumentResultDto>.Fail(ex.Message);
        }

        if (ocrResult.KeptPageIndices.Count == 0)
            return Result<UploadDocumentResultDto>.Fail("No readable text was found in the document.");

        // Save only the pages whose text we kept, preserving order.
        var pagePaths = new List<string>(ocrResult.KeptPageIndices.Count);
        foreach (var i in ocrResult.KeptPageIndices)
        {
            var page = cmd.Pages[i];
            pagePaths.Add(await storage.SaveAsync(page.Content, page.FileName, ct));
        }

        var firstKept = cmd.Pages[ocrResult.KeptPageIndices[0]];
        var doc = new Document
        {
            UserId = cmd.UserId,
            FileName = firstKept.FileName,
            FilePath = pagePaths[0],
            ContentType = firstKept.ContentType,
            OcrText = ocrResult.CombinedText,
            PagePaths = pagePaths.ToArray()
        };
        await documents.AddAsync(doc, ct);

        return Result<UploadDocumentResultDto>.Ok(new UploadDocumentResultDto(
            doc.Id, doc.FileName, doc.ContentType, doc.UploadedAt,
            PageCount: pagePaths.Count, SkippedPages: ocrResult.SkippedCount));
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

        // Remove children first to satisfy foreign keys, then the document and its files.
        await messages.DeleteByDocumentIdAsync(doc.Id, ct);
        await analyses.DeleteByDocumentIdAsync(doc.Id, ct);
        await documents.DeleteAsync(doc.Id, ct);

        // Delete every page file. PagePaths includes page 0 (also in FilePath), so dedupe to avoid a
        // double delete. Legacy single-file rows have empty PagePaths → fall back to FilePath.
        var paths = new HashSet<string>(doc.PagePaths, StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(doc.FilePath)) paths.Add(doc.FilePath);
        foreach (var path in paths)
            await storage.DeleteAsync(path, ct);

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
