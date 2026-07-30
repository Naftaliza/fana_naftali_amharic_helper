using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

public record ListDocumentsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<DocumentSummaryDto>>>;

public class ListDocumentsHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses)
    : IRequestHandler<ListDocumentsQuery, Result<IReadOnlyList<DocumentSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentSummaryDto>>> Handle(ListDocumentsQuery q, CancellationToken ct)
    {
        var docs = await documents.ListByUserAsync(q.UserId, ct);
        var list = new List<DocumentSummaryDto>();
        foreach (var d in docs)
        {
            var analysis = await analyses.GetByDocumentIdAsync(d.Id, ct);
            var deadlines = analysis?.Deadlines
                .Select(dl => new DeadlineDto { Date = dl.Date, Description = dl.Description })
                .ToList() ?? new List<DeadlineDto>();
            list.Add(new DocumentSummaryDto(d.Id, d.FileName, d.ContentType, d.UploadedAt, analysis is not null, d.Status, deadlines));
        }
        return Result<IReadOnlyList<DocumentSummaryDto>>.Ok(list);
    }
}

public record GetDocumentQuery(Guid UserId, Guid DocumentId) : IRequest<Result<DocumentDetailDto>>;

public class GetDocumentHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses)
    : IRequestHandler<GetDocumentQuery, Result<DocumentDetailDto>>
{
    public async Task<Result<DocumentDetailDto>> Handle(GetDocumentQuery q, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(q.DocumentId, ct);
        if (doc is null || doc.UserId != q.UserId)
            return Result<DocumentDetailDto>.Fail("Document not found.");

        var analysis = await analyses.GetByDocumentIdAsync(doc.Id, ct);
        DocumentAnalysisResult? result = analysis is null ? null : new DocumentAnalysisResult
        {
            Summary = analysis.Summary,
            DocumentType = analysis.DocumentType,
            Category = analysis.Category,
            UrgencyLevel = analysis.UrgencyLevel,
            KeyPoints = analysis.KeyPoints.ToList(),
            RequiredActions = analysis.RequiredActions
                .Select(a => new RequiredActionDto { Description = a.Description, IsMandatory = a.IsMandatory }).ToList(),
            Deadlines = analysis.Deadlines
                .Select(d => new DeadlineDto { Date = d.Date, Description = d.Description }).ToList(),
            Explanation = analysis.Explanation
        };

        return Result<DocumentDetailDto>.Ok(
            new DocumentDetailDto(
                doc.Id, doc.FileName, doc.ContentType, doc.OcrText, doc.UploadedAt, result,
                doc.Status, doc.ProcessedPages, doc.TotalPages, doc.SkippedPages, doc.ProcessingError));
    }
}
