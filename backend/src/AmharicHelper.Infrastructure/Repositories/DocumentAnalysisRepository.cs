using System.Text.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class DocumentAnalysisRepository(ISqlConnectionFactory factory) : IDocumentAnalysisRepository
{
    public async Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // Take the most recent analysis; tolerate legacy duplicate rows.
        var row = await conn.QueryFirstOrDefaultAsync<AnalysisRow>(
            "SELECT TOP 1 * FROM dbo.DocumentAnalyses WHERE DocumentId = @documentId ORDER BY CreatedAt DESC",
            new { documentId });
        return row?.ToEntity();
    }

    public async Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.DocumentAnalyses
                (Id, DocumentId, Summary, DocumentType, UrgencyLevel, KeyPointsJson,
                 RequiredActionsJson, DeadlinesJson, TranslatedAmharic, TranslatedSimpleHebrew, CreatedAt)
            VALUES
                (@Id, @DocumentId, @Summary, @DocumentType, @UrgencyLevel, @KeyPointsJson,
                 @RequiredActionsJson, @DeadlinesJson, @TranslatedAmharic, @TranslatedSimpleHebrew, @CreatedAt)
            """,
            new
            {
                analysis.Id,
                analysis.DocumentId,
                analysis.Summary,
                analysis.DocumentType,
                UrgencyLevel = (int)analysis.UrgencyLevel,
                KeyPointsJson = JsonSerializer.Serialize(analysis.KeyPoints),
                RequiredActionsJson = JsonSerializer.Serialize(analysis.RequiredActions),
                DeadlinesJson = JsonSerializer.Serialize(analysis.Deadlines),
                analysis.TranslatedAmharic,
                analysis.TranslatedSimpleHebrew,
                analysis.CreatedAt
            });
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM dbo.DocumentAnalyses WHERE DocumentId = @documentId", new { documentId });
    }

    /// <summary>Raw row matching the SQL columns; JSON columns are deserialized in <see cref="ToEntity"/>.</summary>
    private class AnalysisRow
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public int UrgencyLevel { get; set; }
        public string KeyPointsJson { get; set; } = "[]";
        public string RequiredActionsJson { get; set; } = "[]";
        public string DeadlinesJson { get; set; } = "[]";
        public string TranslatedAmharic { get; set; } = string.Empty;
        public string TranslatedSimpleHebrew { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public DocumentAnalysis ToEntity() => new()
        {
            Id = Id,
            DocumentId = DocumentId,
            Summary = Summary,
            DocumentType = DocumentType,
            UrgencyLevel = (UrgencyLevel)UrgencyLevel,
            KeyPoints = JsonSerializer.Deserialize<List<string>>(KeyPointsJson) ?? new(),
            RequiredActions = JsonSerializer.Deserialize<List<RequiredAction>>(RequiredActionsJson) ?? new(),
            Deadlines = JsonSerializer.Deserialize<List<Deadline>>(DeadlinesJson) ?? new(),
            TranslatedAmharic = TranslatedAmharic,
            TranslatedSimpleHebrew = TranslatedSimpleHebrew,
            CreatedAt = CreatedAt
        };
    }
}
