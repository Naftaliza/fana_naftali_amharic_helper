using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class DocumentRepository(ISqlConnectionFactory factory) : IDocumentRepository
{
    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<DocumentRow>(
            "SELECT * FROM Documents WHERE Id = @id", new { id });
        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<DocumentRow>(
            "SELECT * FROM Documents WHERE UserId = @userId ORDER BY UploadedAt DESC", new { userId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // 0 = Pending, 1 = Processing.
        var rows = await conn.QueryAsync<DocumentRow>(
            "SELECT * FROM Documents WHERE Status IN (0, 1) ORDER BY UploadedAt");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO Documents
                (Id, UserId, FileName, FilePath, ContentType, OcrText, PagePaths, PageContentTypes,
                 Status, ProcessedPages, TotalPages, SkippedPages, ProcessingError, UploadedAt)
            VALUES
                (@Id, @UserId, @FileName, @FilePath, @ContentType, @OcrText, @PagePaths, @PageContentTypes,
                 @Status, @ProcessedPages, @TotalPages, @SkippedPages, @ProcessingError, @UploadedAt)
            """,
            new
            {
                document.Id,
                document.UserId,
                document.FileName,
                document.FilePath,
                document.ContentType,
                document.OcrText,
                document.PagePaths,
                document.PageContentTypes,
                Status = (int)document.Status,
                document.ProcessedPages,
                document.TotalPages,
                document.SkippedPages,
                document.ProcessingError,
                document.UploadedAt
            });
    }

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            UPDATE Documents
            SET OcrText = @OcrText, Status = @Status, ProcessedPages = @ProcessedPages,
                TotalPages = @TotalPages, SkippedPages = @SkippedPages, ProcessingError = @ProcessingError
            WHERE Id = @Id
            """,
            new
            {
                document.Id,
                document.OcrText,
                Status = (int)document.Status,
                document.ProcessedPages,
                document.TotalPages,
                document.SkippedPages,
                document.ProcessingError
            });
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM Documents WHERE Id = @id", new { id });
    }

    /// <summary>Raw row matching the SQL columns; Status is stored as an int (same convention as
    /// DocumentAnalysisRepository/LeadRepository) and converted on the way out.</summary>
    private class DocumentRow
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string? OcrText { get; set; }
        public string[] PagePaths { get; set; } = [];
        public string[] PageContentTypes { get; set; } = [];
        public int Status { get; set; }
        public int ProcessedPages { get; set; }
        public int TotalPages { get; set; }
        public int SkippedPages { get; set; }
        public string? ProcessingError { get; set; }
        public DateTime UploadedAt { get; set; }

        public Document ToEntity() => new()
        {
            Id = Id,
            UserId = UserId,
            FileName = FileName,
            FilePath = FilePath,
            ContentType = ContentType,
            OcrText = OcrText,
            PagePaths = PagePaths,
            PageContentTypes = PageContentTypes,
            Status = (DocumentProcessingStatus)Status,
            ProcessedPages = ProcessedPages,
            TotalPages = TotalPages,
            SkippedPages = SkippedPages,
            ProcessingError = ProcessingError,
            UploadedAt = UploadedAt
        };
    }
}
