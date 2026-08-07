using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class RetryOcrHandlerTests
{
    private sealed class FakeQueue : IDocumentProcessingQueue
    {
        public List<Guid> Enqueued { get; } = new();
        public void Enqueue(Guid documentId) => Enqueued.Add(documentId);
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? ToReturn { get; set; }
        public Document? Updated { get; private set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListExpiredAsync(DateTime nowUtc, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Document document, CancellationToken ct = default) { Updated = document; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Resets_a_failed_document_to_Pending_and_requeues_it()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = docId, UserId = userId, Status = DocumentProcessingStatus.Failed,
                ProcessingError = "No readable text was found in the document.", ProcessedPages = 2,
            },
        };
        var queue = new FakeQueue();
        var handler = new RetryOcrHandler(docs, queue);

        var result = await handler.Handle(new RetryOcrCommand(userId, docId), default);

        Assert.True(result.Success);
        Assert.Equal(DocumentProcessingStatus.Pending, docs.Updated!.Status);
        Assert.Null(docs.Updated.ProcessingError);
        Assert.Equal(0, docs.Updated.ProcessedPages);
        Assert.Single(queue.Enqueued);
        Assert.Equal(docId, queue.Enqueued[0]);
    }

    [Fact]
    public async Task Refuses_to_retry_a_document_that_is_not_Failed()
    {
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docs = new FakeDocs { ToReturn = new Document { Id = docId, UserId = userId, Status = DocumentProcessingStatus.Ready } };
        var handler = new RetryOcrHandler(docs, new FakeQueue());

        var result = await handler.Handle(new RetryOcrCommand(userId, docId), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Refuses_a_document_owned_by_someone_else()
    {
        var docId = Guid.NewGuid();
        var docs = new FakeDocs { ToReturn = new Document { Id = docId, UserId = Guid.NewGuid(), Status = DocumentProcessingStatus.Failed } };
        var handler = new RetryOcrHandler(docs, new FakeQueue());

        var result = await handler.Handle(new RetryOcrCommand(Guid.NewGuid(), docId), default);

        Assert.False(result.Success);
    }
}
