using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class ListDocumentsHandlerTests
{
    private sealed class FakeDocs : IDocumentRepository
    {
        public List<Document> Docs { get; } = new();
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Docs.FirstOrDefault(d => d.Id == id));
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Docs.Where(d => d.UserId == userId).ToList());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) { Docs.Add(document); return Task.CompletedTask; }
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAnalyses : IDocumentAnalysisRepository
    {
        public Dictionary<Guid, DocumentAnalysis> ByDocumentId { get; } = new();
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(ByDocumentId.TryGetValue(documentId, out var a) ? a : null);
        public Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default) { ByDocumentId[analysis.DocumentId] = analysis; return Task.CompletedTask; }
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) { ByDocumentId.Remove(documentId); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Includes_deadlines_for_documents_with_analysis()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docs = new FakeDocs();
        docs.Docs.Add(new Document { Id = docId, UserId = userId, FileName = "letter.pdf" });

        var analyses = new FakeAnalyses();
        var deadlineDate = new DateTime(2026, 8, 12);
        analyses.ByDocumentId[docId] = new DocumentAnalysis
        {
            DocumentId = docId,
            Deadlines = new List<Deadline> { new(deadlineDate, new LocalizedText("שלמו", "ይክፈሉ", "Pay")) }
        };

        var handler = new ListDocumentsHandler(docs, analyses);
        var result = await handler.Handle(new ListDocumentsQuery(userId), default);

        Assert.True(result.Success);
        var summary = Assert.Single(result.Value!);
        var deadline = Assert.Single(summary.Deadlines);
        Assert.Equal(deadlineDate, deadline.Date);
        Assert.Equal("Pay", deadline.Description.En);
    }

    [Fact]
    public async Task Empty_deadlines_for_documents_with_no_analysis_yet()
    {
        var userId = Guid.NewGuid();
        var docs = new FakeDocs();
        docs.Docs.Add(new Document { Id = Guid.NewGuid(), UserId = userId, FileName = "letter.pdf" });

        var handler = new ListDocumentsHandler(docs, new FakeAnalyses());
        var result = await handler.Handle(new ListDocumentsQuery(userId), default);

        Assert.Empty(Assert.Single(result.Value!).Deadlines);
    }
}
