using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class AttachTrialAnalysisHandlerTests
{
    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? Added { get; private set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Document?>(null);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) { Added = document; return Task.CompletedTask; }
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAnalyses : IDocumentAnalysisRepository
    {
        public DocumentAnalysis? Added { get; private set; }
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult<DocumentAnalysis?>(null);
        public Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default) { Added = analysis; return Task.CompletedTask; }
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Creates_a_Ready_document_with_no_pages_from_the_trial_analysis()
    {
        var userId = Guid.NewGuid();
        var docs = new FakeDocs();
        var analyses = new FakeAnalyses();
        var handler = new AttachTrialAnalysisHandler(docs, analyses);

        var analysis = new DocumentAnalysisResult
        {
            Summary = new LocalizedText("תקציר", "ማጠቃለያ", "Summary"),
            DocumentType = new LocalizedText("מכתב", "ደብዳቤ", "Letter"),
            UrgencyLevel = UrgencyLevel.Medium,
            KeyPoints = new() { new LocalizedText("א", "ሀ", "Point") },
            RequiredActions = new() { new RequiredActionDto { Description = new LocalizedText("שלמו", "ይክፈሉ", "Pay"), IsMandatory = true } },
            Deadlines = new() { new DeadlineDto { Date = new DateTime(2026, 8, 12), Description = new LocalizedText("שלמו", "ይክፈሉ", "Pay") } },
            Explanation = new LocalizedText("הסבר", "ማብራሪያ", "Explanation"),
        };

        var result = await handler.Handle(new AttachTrialAnalysisCommand(userId, analysis), default);

        Assert.True(result.Success);
        Assert.Equal(userId, docs.Added!.UserId);
        Assert.Equal(DocumentProcessingStatus.Ready, docs.Added.Status);
        Assert.Empty(docs.Added.PagePaths);
        Assert.Equal(0, docs.Added.TotalPages);
        Assert.Null(docs.Added.OcrText);

        Assert.Equal(docs.Added.Id, analyses.Added!.DocumentId);
        Assert.Equal("Pay", analyses.Added.RequiredActions[0].Description.En);
        Assert.Single(analyses.Added.Deadlines);

        Assert.True(result.Value!.HasAnalysis);
        Assert.Equal(docs.Added.Id, result.Value.Id);
    }
}
