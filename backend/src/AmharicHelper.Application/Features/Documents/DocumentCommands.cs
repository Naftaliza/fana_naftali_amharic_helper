using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

// ---- Upload (one or more pages); OCR runs afterward, off the request thread ----
public record UploadDocumentCommand(Guid UserId, IReadOnlyList<UploadPage> Pages)
    : IRequest<Result<UploadDocumentResultDto>>;

public class UploadDocumentHandler(
    IFileStorage storage,
    IDocumentRepository documents,
    IDocumentProcessingQueue queue,
    IEventTracker events) : IRequestHandler<UploadDocumentCommand, Result<UploadDocumentResultDto>>
{
    public async Task<Result<UploadDocumentResultDto>> Handle(UploadDocumentCommand cmd, CancellationToken ct)
    {
        if (cmd.Pages.Count == 0)
            return Result<UploadDocumentResultDto>.Fail("No pages provided.");

        // Save every page immediately — all of them, not just ones that will turn out readable,
        // since we don't know that yet. OCR itself now runs in DocumentProcessor, off this request,
        // so a multi-page upload no longer holds the HTTP connection open for minutes.
        var pagePaths = new string[cmd.Pages.Count];
        var pageContentTypes = new string[cmd.Pages.Count];
        for (var i = 0; i < cmd.Pages.Count; i++)
        {
            pagePaths[i] = await storage.SaveAsync(cmd.Pages[i].Content, cmd.Pages[i].FileName, ct);
            pageContentTypes[i] = cmd.Pages[i].ContentType;
        }

        var first = cmd.Pages[0];
        var doc = new Document
        {
            UserId = cmd.UserId,
            FileName = first.FileName,
            FilePath = pagePaths[0],
            ContentType = first.ContentType,
            PagePaths = pagePaths,
            PageContentTypes = pageContentTypes,
            Status = DocumentProcessingStatus.Pending,
            TotalPages = cmd.Pages.Count
        };
        await documents.AddAsync(doc, ct);
        queue.Enqueue(doc.Id);
        await events.TrackAsync(EventNames.DocumentUploaded, cmd.UserId, ct);

        return Result<UploadDocumentResultDto>.Ok(new UploadDocumentResultDto(
            doc.Id, doc.FileName, doc.ContentType, doc.UploadedAt,
            Status: doc.Status, TotalPages: doc.TotalPages));
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
    IAiProvider ai,
    IEventTracker events) : IRequestHandler<AnalyzeDocumentCommand, Result<DocumentAnalysisResult>>
{
    public async Task<Result<DocumentAnalysisResult>> Handle(AnalyzeDocumentCommand cmd, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null || doc.UserId != cmd.UserId)
            return Result<DocumentAnalysisResult>.Fail("Document not found.");
        if (doc.Status is DocumentProcessingStatus.Pending or DocumentProcessingStatus.Processing)
            return Result<DocumentAnalysisResult>.Fail("Document is still being processed. Try again shortly.");
        if (doc.Status == DocumentProcessingStatus.Failed || string.IsNullOrWhiteSpace(doc.OcrText))
            return Result<DocumentAnalysisResult>.Fail(doc.ProcessingError ?? "Document has no extracted text to analyze.");

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
        await events.TrackAsync(EventNames.DocumentAnalyzed, cmd.UserId, ct);

        return Result<DocumentAnalysisResult>.Ok(result);
    }
}

// ---- Retry OCR (a Failed document only) ----
public record RetryOcrCommand(Guid UserId, Guid DocumentId) : IRequest<Result<bool>>;

public class RetryOcrHandler(
    IDocumentRepository documents,
    IDocumentProcessingQueue queue) : IRequestHandler<RetryOcrCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RetryOcrCommand cmd, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(cmd.DocumentId, ct);
        if (doc is null || doc.UserId != cmd.UserId)
            return Result<bool>.Fail("Document not found.");
        if (doc.Status != DocumentProcessingStatus.Failed)
            return Result<bool>.Fail("Only a failed document can be retried.");

        // Same reset DocumentProcessor itself would see on a fresh upload — the in-memory queue
        // doesn't survive a restart (see DocumentProcessingWorker's own startup reconciliation,
        // which uses this identical Enqueue call), so explicitly re-queuing here is required.
        doc.Status = DocumentProcessingStatus.Pending;
        doc.ProcessedPages = 0;
        doc.ProcessingError = null;
        await documents.UpdateAsync(doc, ct);
        queue.Enqueue(doc.Id);

        return Result<bool>.Ok(true);
    }
}

// ---- Attach a previously-computed anonymous trial analysis to the now-authenticated account.
// No OCR text or page files exist for a trial result (the anonymous /api/trial/analyze endpoint
// never persists either) — this only saves the analysis itself, so the user keeps the
// explanation/actions/deadlines instead of losing it entirely at the signup moment. ----
public record AttachTrialAnalysisCommand(Guid UserId, DocumentAnalysisResult Analysis)
    : IRequest<Result<DocumentSummaryDto>>;

public class AttachTrialAnalysisHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses) : IRequestHandler<AttachTrialAnalysisCommand, Result<DocumentSummaryDto>>
{
    public async Task<Result<DocumentSummaryDto>> Handle(AttachTrialAnalysisCommand cmd, CancellationToken ct)
    {
        var typeLabel = cmd.Analysis.DocumentType.En;
        var doc = new Document
        {
            UserId = cmd.UserId,
            FileName = string.IsNullOrWhiteSpace(typeLabel) ? "Saved analysis" : typeLabel,
            ContentType = "application/octet-stream",
            PagePaths = Array.Empty<string>(),
            PageContentTypes = Array.Empty<string>(),
            Status = DocumentProcessingStatus.Ready,
            TotalPages = 0,
        };
        await documents.AddAsync(doc, ct);

        await analyses.AddAsync(new DocumentAnalysis
        {
            DocumentId = doc.Id,
            Summary = cmd.Analysis.Summary,
            DocumentType = cmd.Analysis.DocumentType,
            Category = cmd.Analysis.Category,
            UrgencyLevel = cmd.Analysis.UrgencyLevel,
            KeyPoints = cmd.Analysis.KeyPoints.ToList(),
            RequiredActions = cmd.Analysis.RequiredActions
                .Select(a => new RequiredAction(a.Description, a.IsMandatory)).ToList(),
            Deadlines = cmd.Analysis.Deadlines
                .Select(d => new Deadline(d.Date, d.Description)).ToList(),
            Explanation = cmd.Analysis.Explanation,
        }, ct);

        return Result<DocumentSummaryDto>.Ok(new DocumentSummaryDto(
            doc.Id, doc.FileName, doc.ContentType, doc.UploadedAt, true, doc.Status, cmd.Analysis.Deadlines));
    }
}
