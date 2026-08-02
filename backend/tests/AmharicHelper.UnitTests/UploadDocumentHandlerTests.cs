using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class UploadDocumentHandlerTests
{
    // ---- Test doubles (the project has no mocking library) ----

    private sealed class FakeStorage : IFileStorage
    {
        public List<string> Saved { get; } = new();
        public List<string> Deleted { get; } = new();
        public Dictionary<string, byte[]> Contents { get; } = new();
        public Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default)
        {
            var path = $"/store/{Saved.Count}_{fileName}";
            Saved.Add(path);
            Contents[path] = content;
            return Task.FromResult(path);
        }
        public Task<byte[]> ReadAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(Contents.TryGetValue(path, out var b) ? b : Array.Empty<byte>());
        public Task DeleteAsync(string path, CancellationToken ct = default) { Deleted.Add(path); return Task.CompletedTask; }
    }

    private sealed class FakeQueue : IDocumentProcessingQueue
    {
        public List<Guid> Enqueued { get; } = new();
        public void Enqueue(Guid documentId) => Enqueued.Add(documentId);
    }

    private sealed class NoopEventTracker : IEventTracker
    {
        public Task TrackAsync(string eventName, Guid? userId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? Added { get; private set; }
        public Document? ToReturn { get; set; }
        public List<Guid> DeletedIds { get; } = new();
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListExpiredAsync(DateTime nowUtc, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) { Added = document; return Task.CompletedTask; }
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { DeletedIds.Add(id); return Task.CompletedTask; }
    }

    private sealed class NoopChat : IChatMessageRepository
    {
        public Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid d, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        public Task AddAsync(ChatMessage m, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid d, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopAnalyses : IDocumentAnalysisRepository
    {
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid d, CancellationToken ct = default) => Task.FromResult<DocumentAnalysis?>(null);
        public Task AddAsync(DocumentAnalysis a, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid d, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static UploadPage Page(byte id) => new($"p{id}.jpg", "image/jpeg", new byte[] { id });

    // ---- Tests: upload now just saves every page and queues processing — OCR itself is
    // DocumentProcessor's job, tested separately in DocumentProcessorTests. ----

    [Fact]
    public async Task Saves_every_page_and_starts_Pending()
    {
        var storage = new FakeStorage(); var docs = new FakeDocs(); var queue = new FakeQueue();
        var handler = new UploadDocumentHandler(storage, docs, queue, new NoopEventTracker());

        var result = await handler.Handle(
            new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(1), Page(2), Page(3) }), default);

        Assert.True(result.Success);
        Assert.Equal(3, storage.Saved.Count);
        Assert.Equal(3, docs.Added!.PagePaths.Length);
        Assert.Equal(3, docs.Added.PageContentTypes.Length);
        Assert.Equal(DocumentProcessingStatus.Pending, docs.Added.Status);
        Assert.Equal(3, docs.Added.TotalPages);
        Assert.Equal(DocumentProcessingStatus.Pending, result.Value!.Status);
        Assert.Equal(3, result.Value.TotalPages);
    }

    [Fact]
    public async Task Queues_the_new_document_for_background_processing()
    {
        var queue = new FakeQueue();
        var handler = new UploadDocumentHandler(new FakeStorage(), new FakeDocs(), queue, new NoopEventTracker());
        var result = await handler.Handle(new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(1) }), default);

        Assert.True(result.Success);
        Assert.Single(queue.Enqueued);
        Assert.Equal(result.Value!.Id, queue.Enqueued[0]);
    }

    [Fact]
    public async Task Empty_page_list_fails()
    {
        var handler = new UploadDocumentHandler(new FakeStorage(), new FakeDocs(), new FakeQueue(), new NoopEventTracker());
        var result = await handler.Handle(new UploadDocumentCommand(Guid.NewGuid(), Array.Empty<UploadPage>()), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Delete_removes_every_deduped_page_file()
    {
        var storage = new FakeStorage();
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Empty,
                FilePath = "/store/0_p1.jpg",
                PagePaths = new[] { "/store/0_p1.jpg", "/store/1_p2.jpg" } // page 0 in both
            }
        };
        var handler = new DeleteDocumentHandler(docs, new NoopAnalyses(), new NoopChat(), storage);

        var result = await handler.Handle(new DeleteDocumentCommand(Guid.Empty, docs.ToReturn.Id), default);

        Assert.True(result.Success);
        Assert.Equal(2, storage.Deleted.Count);                 // deduped: not 3
        Assert.Contains("/store/0_p1.jpg", storage.Deleted);
        Assert.Contains("/store/1_p2.jpg", storage.Deleted);
    }

    [Fact]
    public async Task Delete_legacy_document_falls_back_to_FilePath()
    {
        var storage = new FakeStorage();
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Empty,
                FilePath = "/store/legacy.jpg",
                PagePaths = Array.Empty<string>()               // legacy single-file row
            }
        };
        var handler = new DeleteDocumentHandler(docs, new NoopAnalyses(), new NoopChat(), storage);

        await handler.Handle(new DeleteDocumentCommand(Guid.Empty, docs.ToReturn.Id), default);

        Assert.Single(storage.Deleted);
        Assert.Equal("/store/legacy.jpg", storage.Deleted[0]);
    }
}
