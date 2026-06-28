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
            "SELECT * FROM DocumentAnalyses WHERE DocumentId = @documentId ORDER BY CreatedAt DESC LIMIT 1",
            new { documentId });
        return row?.ToEntity();
    }

    public async Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO DocumentAnalyses
                (Id, DocumentId, Summary, DocumentType, Category, UrgencyLevel, KeyPointsJson,
                 RequiredActionsJson, DeadlinesJson, ExplanationJson, CreatedAt)
            VALUES
                (@Id, @DocumentId, @Summary, @DocumentType, @Category, @UrgencyLevel, @KeyPointsJson,
                 @RequiredActionsJson, @DeadlinesJson, @ExplanationJson, @CreatedAt)
            """,
            new
            {
                analysis.Id,
                analysis.DocumentId,
                Summary = JsonSerializer.Serialize(analysis.Summary),
                DocumentType = JsonSerializer.Serialize(analysis.DocumentType),
                Category = (int)analysis.Category,
                UrgencyLevel = (int)analysis.UrgencyLevel,
                KeyPointsJson = JsonSerializer.Serialize(analysis.KeyPoints),
                RequiredActionsJson = JsonSerializer.Serialize(analysis.RequiredActions),
                DeadlinesJson = JsonSerializer.Serialize(analysis.Deadlines),
                ExplanationJson = JsonSerializer.Serialize(analysis.Explanation),
                analysis.CreatedAt
            });
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM DocumentAnalyses WHERE DocumentId = @documentId", new { documentId });
    }

    /// <summary>Raw row matching the SQL columns; JSON columns are deserialized in <see cref="ToEntity"/>.</summary>
    private class AnalysisRow
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Summary { get; set; } = "{}";
        public string DocumentType { get; set; } = "{}";
        public int Category { get; set; }
        public int UrgencyLevel { get; set; }
        public string KeyPointsJson { get; set; } = "[]";
        public string RequiredActionsJson { get; set; } = "[]";
        public string DeadlinesJson { get; set; } = "[]";
        public string ExplanationJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; }

        public DocumentAnalysis ToEntity() => new()
        {
            Id = Id,
            DocumentId = DocumentId,
            Summary = JsonSerializer.Deserialize<LocalizedText>(Summary) ?? new(),
            DocumentType = JsonSerializer.Deserialize<LocalizedText>(DocumentType) ?? new(),
            Category = (DocumentCategory)Category,
            UrgencyLevel = (UrgencyLevel)UrgencyLevel,
            KeyPoints = JsonSerializer.Deserialize<List<LocalizedText>>(KeyPointsJson) ?? new(),
            RequiredActions = JsonSerializer.Deserialize<List<RequiredAction>>(RequiredActionsJson) ?? new(),
            Deadlines = JsonSerializer.Deserialize<List<Deadline>>(DeadlinesJson) ?? new(),
            Explanation = JsonSerializer.Deserialize<LocalizedText>(ExplanationJson) ?? new(),
            CreatedAt = CreatedAt
        };
    }
}
