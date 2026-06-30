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

    /// <summary>OCR fake keyed by the page's content type tag (we encode behavior in the bytes).</summary>
    private sealed class FakeOcr : IOcrProvider
    {
        // Map: page marker byte -> result. 1=text, 0=blank(empty), 2=throw transient, 3=throw config.
        public List<byte[]> Seen { get; } = new();

        public Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default)
        {
            Seen.Add(fileBytes);
            return fileBytes[0] switch
            {
                1 => Task.FromResult($"text-{fileBytes[1]}"),
                0 => Task.FromResult(""),                                   // blank page
                2 => throw new InvalidOperationException("OCR failed: 500 InternalServerError"),
                3 => throw new InvalidOperationException("Anthropic API key is not configured (Ai:AnthropicApiKey)."),
                _ => Task.FromResult("?"),
            };
        }
    }

    private sealed class FakeStorage : IFileStorage
    {
        public List<string> Saved { get; } = new();
        public List<string> Deleted { get; } = new();
        public Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default)
        {
            var path = $"/store/{Saved.Count}_{fileName}";
            Saved.Add(path);
            return Task.FromResult(path);
        }
        public Task<byte[]> ReadAsync(string path, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task DeleteAsync(string path, CancellationToken ct = default) { Deleted.Add(path); return Task.CompletedTask; }
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? Added { get; private set; }
        public Document? ToReturn { get; set; }
        public List<Guid> DeletedIds { get; } = new();
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
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

    private static UploadPage Page(byte marker, byte id = 0) =>
        new($"p{id}.jpg", "image/jpeg", new byte[] { marker, id });

    // ---- Tests ----

    [Fact]
    public async Task Single_page_has_no_page_header_and_one_file()
    {
        var ocr = new FakeOcr(); var storage = new FakeStorage(); var docs = new FakeDocs();
        var handler = new UploadDocumentHandler(storage, ocr, docs);

        var result = await handler.Handle(new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(1, 7) }), default);

        Assert.True(result.Success);
        Assert.Equal("text-7", docs.Added!.OcrText);          // no "--- Page ---" header
        Assert.Single(storage.Saved);
        Assert.Single(docs.Added.PagePaths);
        Assert.Equal(0, result.Value!.SkippedPages);
        Assert.Equal(1, result.Value.PageCount);
    }

    [Fact]
    public async Task Multiple_pages_are_numbered_and_concatenated_in_order()
    {
        var ocr = new FakeOcr(); var storage = new FakeStorage(); var docs = new FakeDocs();
        var handler = new UploadDocumentHandler(storage, ocr, docs);

        var result = await handler.Handle(
            new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(1, 1), Page(1, 2), Page(1, 3) }), default);

        Assert.True(result.Success);
        Assert.Equal("--- Page 1 ---\n\ntext-1\n\n--- Page 2 ---\n\ntext-2\n\n--- Page 3 ---\n\ntext-3",
            docs.Added!.OcrText);
        Assert.Equal(3, storage.Saved.Count);
        Assert.Equal(3, docs.Added.PagePaths.Length);
    }

    [Fact]
    public async Task Blank_page_is_skipped_and_remaining_pages_renumbered()
    {
        var ocr = new FakeOcr(); var storage = new FakeStorage(); var docs = new FakeDocs();
        var handler = new UploadDocumentHandler(storage, ocr, docs);

        var result = await handler.Handle(
            new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(1, 1), Page(0, 2), Page(1, 3) }), default);

        Assert.True(result.Success);
        Assert.Equal("--- Page 1 ---\n\ntext-1\n\n--- Page 2 ---\n\ntext-3", docs.Added!.OcrText);
        Assert.Equal(2, storage.Saved.Count);               // only the two readable pages saved
        Assert.Equal(1, result.Value!.SkippedPages);
    }

    [Fact]
    public async Task Transient_page_failure_is_skipped_but_batch_continues()
    {
        var ocr = new FakeOcr(); var storage = new FakeStorage(); var docs = new FakeDocs();
        var handler = new UploadDocumentHandler(storage, ocr, docs);

        var result = await handler.Handle(
            new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(2, 1), Page(1, 2) }), default);

        Assert.True(result.Success);
        Assert.Equal("text-2", docs.Added!.OcrText);
        Assert.Equal(1, result.Value!.SkippedPages);
    }

    [Fact]
    public async Task All_pages_blank_fails_and_saves_nothing()
    {
        var ocr = new FakeOcr(); var storage = new FakeStorage(); var docs = new FakeDocs();
        var handler = new UploadDocumentHandler(storage, ocr, docs);

        var result = await handler.Handle(
            new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(0, 1), Page(0, 2) }), default);

        Assert.False(result.Success);
        Assert.Empty(storage.Saved);
        Assert.Null(docs.Added);
    }

    [Fact]
    public async Task Config_error_fails_whole_batch_and_saves_nothing()
    {
        var ocr = new FakeOcr(); var storage = new FakeStorage(); var docs = new FakeDocs();
        var handler = new UploadDocumentHandler(storage, ocr, docs);

        var result = await handler.Handle(
            new UploadDocumentCommand(Guid.NewGuid(), new[] { Page(3, 1), Page(1, 2) }), default);

        Assert.False(result.Success);
        Assert.Contains("API key", result.Error);
        Assert.Empty(storage.Saved);   // critically: no orphaned files for a doomed batch
        Assert.Null(docs.Added);
    }

    [Fact]
    public async Task Empty_page_list_fails()
    {
        var handler = new UploadDocumentHandler(new FakeStorage(), new FakeOcr(), new FakeDocs());
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
