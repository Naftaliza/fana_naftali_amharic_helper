using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Users;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class UserAccountFeatureTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ToReturn { get; set; }
        public List<Guid> Deleted { get; } = new();
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Deleted.Add(id); return Task.CompletedTask; }
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public List<Document> Docs { get; set; } = new();
        public List<Guid> Deleted { get; } = new();
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Docs.FirstOrDefault(d => d.Id == id));
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Docs.Where(d => d.UserId == userId).ToList());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListExpiredAsync(DateTime nowUtc, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Deleted.Add(id); return Task.CompletedTask; }
    }

    private sealed class FakeAnalysisRepository : IDocumentAnalysisRepository
    {
        public Dictionary<Guid, DocumentAnalysis> ByDocumentId { get; } = new();
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(ByDocumentId.TryGetValue(documentId, out var a) ? a : null);
        public Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChatRepository : IChatMessageRepository
    {
        public Dictionary<Guid, List<ChatMessage>> ByDocumentId { get; } = new();
        public Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChatMessage>>(ByDocumentId.TryGetValue(documentId, out var m) ? m : Array.Empty<ChatMessage>());
        public Task AddAsync(ChatMessage message, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : IFileStorage
    {
        public List<string> Deleted { get; } = new();
        public Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default) => Task.FromResult("");
        public Task<byte[]> ReadAsync(string path, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task DeleteAsync(string path, CancellationToken ct = default) { Deleted.Add(path); return Task.CompletedTask; }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<Guid> DeletedForUser { get; } = new();
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(null);
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default) { DeletedForUser.Add(userId); return Task.CompletedTask; }
    }

    private static readonly Guid UserId = Guid.NewGuid();

    // ---- Export ----

    [Fact]
    public async Task Export_fails_for_unknown_user()
    {
        var handler = new ExportAccountHandler(
            new FakeUserRepository(), new FakeDocumentRepository(), new FakeAnalysisRepository(), new FakeChatRepository());

        var result = await handler.Handle(new ExportAccountQuery(UserId), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Export_includes_ocr_text_analysis_and_chat_per_document()
    {
        var docWithAnalysis = new Document { Id = Guid.NewGuid(), UserId = UserId, FileName = "a.jpg", OcrText = "raw text" };
        var docWithoutAnalysis = new Document { Id = Guid.NewGuid(), UserId = UserId, FileName = "b.jpg" };

        var users = new FakeUserRepository { ToReturn = new User { Id = UserId, Email = "u@test.local", DisplayName = "U" } };
        var docs = new FakeDocumentRepository { Docs = { docWithAnalysis, docWithoutAnalysis } };
        var analyses = new FakeAnalysisRepository
        {
            ByDocumentId = { [docWithAnalysis.Id] = new DocumentAnalysis { DocumentId = docWithAnalysis.Id, UrgencyLevel = UrgencyLevel.High } }
        };
        var chat = new FakeChatRepository
        {
            ByDocumentId = { [docWithAnalysis.Id] = new List<ChatMessage> { new() { DocumentId = docWithAnalysis.Id, Role = ChatRole.User, Content = "hi?" } } }
        };
        var handler = new ExportAccountHandler(users, docs, analyses, chat);

        var result = await handler.Handle(new ExportAccountQuery(UserId), default);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Documents.Count);
        var exportedWithAnalysis = result.Value.Documents.Single(d => d.Id == docWithAnalysis.Id);
        Assert.Equal("raw text", exportedWithAnalysis.OcrText);
        Assert.NotNull(exportedWithAnalysis.Analysis);
        Assert.Equal(UrgencyLevel.High, exportedWithAnalysis.Analysis!.UrgencyLevel);
        Assert.Single(exportedWithAnalysis.ChatMessages);
        var exportedWithoutAnalysis = result.Value.Documents.Single(d => d.Id == docWithoutAnalysis.Id);
        Assert.Null(exportedWithoutAnalysis.Analysis);
        Assert.Empty(exportedWithoutAnalysis.ChatMessages);
    }

    // ---- Delete account ----

    [Fact]
    public async Task Delete_fails_for_unknown_user()
    {
        var handler = new DeleteAccountHandler(
            new FakeUserRepository(), new FakeDocumentRepository(), new FakeRefreshTokenRepository(), new FakeStorage());

        var result = await handler.Handle(new DeleteAccountCommand(UserId), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Delete_removes_every_document_its_files_refresh_tokens_and_the_user()
    {
        var doc1 = new Document { Id = Guid.NewGuid(), UserId = UserId, FilePath = "/store/1.jpg", PagePaths = new[] { "/store/1.jpg" } };
        var doc2 = new Document { Id = Guid.NewGuid(), UserId = UserId, FilePath = "/store/2.jpg", PagePaths = new[] { "/store/2.jpg", "/store/2b.jpg" } };

        var users = new FakeUserRepository { ToReturn = new User { Id = UserId, Email = "u@test.local" } };
        var docs = new FakeDocumentRepository { Docs = { doc1, doc2 } };
        var storage = new FakeStorage();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = new DeleteAccountHandler(users, docs, refreshTokens, storage);

        var result = await handler.Handle(new DeleteAccountCommand(UserId), default);

        Assert.True(result.Success);
        Assert.Equal(2, docs.Deleted.Count);
        Assert.Contains(doc1.Id, docs.Deleted);
        Assert.Contains(doc2.Id, docs.Deleted);
        Assert.Equal(3, storage.Deleted.Count); // 1 + 2 page files
        Assert.Single(refreshTokens.DeletedForUser);
        Assert.Single(users.Deleted);
        Assert.Equal(UserId, users.Deleted[0]);
    }
}
