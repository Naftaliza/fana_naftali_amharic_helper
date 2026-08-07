using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Entities;
using Xunit;

namespace AmharicHelper.UnitTests;

public class GetDocumentPageHandlerTests
{
    // ---- Test doubles (the project has no mocking library) ----

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Contents { get; } = new();
        public Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<byte[]> ReadAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(Contents.TryGetValue(path, out var b) ? b : Array.Empty<byte>());
        public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? ToReturn { get; set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListExpiredAsync(DateTime nowUtc, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly Guid UserId = Guid.NewGuid();

    private static (FakeStorage Storage, FakeDocs Docs) MultiPageDoc()
    {
        var storage = new FakeStorage();
        storage.Contents["/store/0_p1.jpg"] = new byte[] { 1 };
        storage.Contents["/store/1_p2.jpg"] = new byte[] { 2 };
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                FilePath = "/store/0_p1.jpg",
                ContentType = "image/jpeg",
                PagePaths = new[] { "/store/0_p1.jpg", "/store/1_p2.jpg" },
                PageContentTypes = new[] { "image/jpeg", "application/pdf" },
            }
        };
        return (storage, docs);
    }

    [Fact]
    public async Task Returns_the_requested_pages_bytes_and_content_type()
    {
        var (storage, docs) = MultiPageDoc();
        var handler = new GetDocumentPageHandler(docs, storage);

        var result = await handler.Handle(new GetDocumentPageQuery(UserId, docs.ToReturn!.Id, 1), default);

        Assert.True(result.Success);
        Assert.Equal(new byte[] { 2 }, result.Value!.Content);
        Assert.Equal("application/pdf", result.Value.ContentType);
    }

    [Fact]
    public async Task Out_of_range_index_fails()
    {
        var (storage, docs) = MultiPageDoc();
        var handler = new GetDocumentPageHandler(docs, storage);

        var result = await handler.Handle(new GetDocumentPageQuery(UserId, docs.ToReturn!.Id, 2), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Another_users_document_fails()
    {
        var (storage, docs) = MultiPageDoc();
        var handler = new GetDocumentPageHandler(docs, storage);

        var result = await handler.Handle(new GetDocumentPageQuery(Guid.NewGuid(), docs.ToReturn!.Id, 0), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Missing_document_fails()
    {
        var docs = new FakeDocs { ToReturn = null };
        var handler = new GetDocumentPageHandler(docs, new FakeStorage());

        var result = await handler.Handle(new GetDocumentPageQuery(UserId, Guid.NewGuid(), 0), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Legacy_document_with_empty_PagePaths_falls_back_to_FilePath_at_index_zero()
    {
        var storage = new FakeStorage();
        storage.Contents["/store/legacy.jpg"] = new byte[] { 9 };
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                FilePath = "/store/legacy.jpg",
                ContentType = "image/jpeg",
                PagePaths = Array.Empty<string>(),
            }
        };
        var handler = new GetDocumentPageHandler(docs, storage);

        var result = await handler.Handle(new GetDocumentPageQuery(UserId, docs.ToReturn.Id, 0), default);

        Assert.True(result.Success);
        Assert.Equal(new byte[] { 9 }, result.Value!.Content);
    }

    [Fact]
    public async Task Legacy_document_rejects_any_index_other_than_zero()
    {
        var storage = new FakeStorage();
        var docs = new FakeDocs
        {
            ToReturn = new Document
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                FilePath = "/store/legacy.jpg",
                ContentType = "image/jpeg",
                PagePaths = Array.Empty<string>(),
            }
        };
        var handler = new GetDocumentPageHandler(docs, storage);

        var result = await handler.Handle(new GetDocumentPageQuery(UserId, docs.ToReturn.Id, 1), default);

        Assert.False(result.Success);
    }
}
