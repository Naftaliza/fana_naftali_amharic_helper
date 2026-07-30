using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Documents;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class DocumentProcessorTests
{
    // ---- Test doubles (the project has no mocking library) ----

    /// <summary>OCR fake keyed by the page's content type tag (we encode behavior in the bytes).</summary>
    private sealed class FakeOcr : IOcrProvider
    {
        // Map: page marker byte -> result. 1=text, 0=blank(empty), 2=throw transient,
        // 3=throw config, 4=throw account-level service error (quota/rate-limit/outage).
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
                4 => throw new InvalidOperationException("OCR service is temporarily unavailable (429)."),
                _ => Task.FromResult("?"),
            };
        }
    }

    private sealed class FakeStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();
        public string Put(byte[] content)
        {
            var path = $"/store/{_files.Count}";
            _files[path] = content;
            return path;
        }
        public Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default) => Task.FromResult(Put(content));
        public Task<byte[]> ReadAsync(string path, CancellationToken ct = default) => Task.FromResult(_files[path]);
        public Task DeleteAsync(string path, CancellationToken ct = default) { _files.Remove(path); return Task.CompletedTask; }
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? Stored { get; set; }
        public int UpdateCount { get; private set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Stored);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) { Stored = document; return Task.CompletedTask; }
        public Task UpdateAsync(Document document, CancellationToken ct = default)
        {
            UpdateCount++;
            Stored = document; // the real repo persists by Id; our fake just tracks the same instance
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (FakeDocs docs, FakeStorage storage, Document doc) SetUp(params byte[] markers)
    {
        var storage = new FakeStorage();
        var paths = markers.Select((m, i) => storage.Put(new byte[] { m, (byte)i })).ToArray();
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FileName = "p0.jpg",
            FilePath = paths[0],
            ContentType = "image/jpeg",
            PagePaths = paths,
            PageContentTypes = paths.Select(_ => "image/jpeg").ToArray(),
            Status = DocumentProcessingStatus.Pending,
            TotalPages = markers.Length
        };
        var docs = new FakeDocs { Stored = doc };
        return (docs, storage, doc);
    }

    // ---- Tests ----

    [Fact]
    public async Task Single_page_has_no_page_header()
    {
        var (docs, storage, doc) = SetUp(1);
        var processor = new DocumentProcessor(docs, storage, new FakeOcr());

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Ready, docs.Stored!.Status);
        Assert.Equal("text-0", docs.Stored.OcrText);          // no "--- Page ---" header
        Assert.Equal(0, docs.Stored.SkippedPages);
        Assert.Equal(1, docs.Stored.ProcessedPages);
    }

    [Fact]
    public async Task Multiple_pages_are_numbered_and_concatenated_in_order()
    {
        var (docs, storage, doc) = SetUp(1, 1, 1);
        var processor = new DocumentProcessor(docs, storage, new FakeOcr());

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Ready, docs.Stored!.Status);
        Assert.Equal("--- Page 1 ---\n\ntext-0\n\n--- Page 2 ---\n\ntext-1\n\n--- Page 3 ---\n\ntext-2",
            docs.Stored.OcrText);
        Assert.Equal(3, docs.Stored.ProcessedPages);
    }

    [Fact]
    public async Task Blank_page_is_skipped_and_remaining_pages_renumbered()
    {
        var (docs, storage, doc) = SetUp(1, 0, 1);
        var processor = new DocumentProcessor(docs, storage, new FakeOcr());

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Ready, docs.Stored!.Status);
        Assert.Equal("--- Page 1 ---\n\ntext-0\n\n--- Page 2 ---\n\ntext-2", docs.Stored.OcrText);
        Assert.Equal(1, docs.Stored.SkippedPages);
    }

    [Fact]
    public async Task Transient_page_failure_is_skipped_but_document_continues()
    {
        var (docs, storage, doc) = SetUp(2, 1);
        var processor = new DocumentProcessor(docs, storage, new FakeOcr());

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Ready, docs.Stored!.Status);
        Assert.Equal("text-1", docs.Stored.OcrText);
        Assert.Equal(1, docs.Stored.SkippedPages);
    }

    [Fact]
    public async Task All_pages_blank_fails_the_document()
    {
        var (docs, storage, doc) = SetUp(0, 0);
        var processor = new DocumentProcessor(docs, storage, new FakeOcr());

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Failed, docs.Stored!.Status);
        Assert.Null(docs.Stored.OcrText);
        Assert.NotNull(docs.Stored.ProcessingError);
    }

    [Fact]
    public async Task Config_error_fails_the_document_and_stops_processing_further_pages()
    {
        var (docs, storage, doc) = SetUp(3, 1);
        var ocr = new FakeOcr();
        var processor = new DocumentProcessor(docs, storage, ocr);

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Failed, docs.Stored!.Status);
        Assert.Contains("API key", docs.Stored.ProcessingError);
        Assert.Single(ocr.Seen);   // the second page is never attempted once a config error hits
    }

    [Fact]
    public async Task Account_level_service_error_fails_the_document_with_an_accurate_message_and_stops_processing_further_pages()
    {
        var (docs, storage, doc) = SetUp(4, 1);
        var ocr = new FakeOcr();
        var processor = new DocumentProcessor(docs, storage, ocr);

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(DocumentProcessingStatus.Failed, docs.Stored!.Status);
        Assert.Contains("temporarily unavailable", docs.Stored.ProcessingError);
        Assert.DoesNotContain("No readable text", docs.Stored.ProcessingError);
        Assert.Single(ocr.Seen);   // the second page is never attempted once a service error hits
    }

    [Fact]
    public async Task Already_Ready_document_is_not_reprocessed()
    {
        var (docs, storage, doc) = SetUp(1);
        doc.Status = DocumentProcessingStatus.Ready;
        doc.OcrText = "already done";
        var processor = new DocumentProcessor(docs, storage, new FakeOcr());

        await processor.ProcessAsync(doc.Id, default);

        Assert.Equal(0, docs.UpdateCount);
        Assert.Equal("already done", docs.Stored!.OcrText);
    }

    [Fact]
    public async Task Missing_document_is_a_noop()
    {
        var docs = new FakeDocs { Stored = null };
        var processor = new DocumentProcessor(docs, new FakeStorage(), new FakeOcr());

        await processor.ProcessAsync(Guid.NewGuid(), default);

        Assert.Equal(0, docs.UpdateCount);
    }
}
