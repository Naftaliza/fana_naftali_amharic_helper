using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

/// <summary>The raw bytes of one uploaded page, for the frontend to show the user what they
/// photographed alongside the AI's analysis of it.</summary>
public record DocumentPageFile(byte[] Content, string ContentType);

/// <summary>Fetch one page's stored file. Index is 0-based, matching the order the pages were
/// uploaded/OCR'd in.</summary>
public record GetDocumentPageQuery(Guid UserId, Guid DocumentId, int Index) : IRequest<Result<DocumentPageFile>>;

public class GetDocumentPageHandler(IDocumentRepository documents, IFileStorage storage)
    : IRequestHandler<GetDocumentPageQuery, Result<DocumentPageFile>>
{
    public async Task<Result<DocumentPageFile>> Handle(GetDocumentPageQuery q, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(q.DocumentId, ct);
        if (doc is null || doc.UserId != q.UserId)
            return Result<DocumentPageFile>.Fail("Document not found.");

        // Legacy single-file documents (uploaded before PagePaths existed) have an empty
        // PagePaths — fall back to the single FilePath/ContentType for index 0 only.
        string path;
        string contentType;
        if (doc.PagePaths.Length > 0)
        {
            if (q.Index < 0 || q.Index >= doc.PagePaths.Length)
                return Result<DocumentPageFile>.Fail("Page not found.");
            path = doc.PagePaths[q.Index];
            contentType = q.Index < doc.PageContentTypes.Length ? doc.PageContentTypes[q.Index] : doc.ContentType;
        }
        else
        {
            if (q.Index != 0)
                return Result<DocumentPageFile>.Fail("Page not found.");
            path = doc.FilePath;
            contentType = doc.ContentType;
        }

        var content = await storage.ReadAsync(path, ct);
        return Result<DocumentPageFile>.Ok(new DocumentPageFile(content, contentType));
    }
}
