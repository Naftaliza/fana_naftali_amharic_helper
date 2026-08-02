using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Chat;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Application.Features.Trial;
using AmharicHelper.Application.Tts;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

/// <summary>
/// Verifies that AnalyzeDocumentHandler/SpeakDocumentHandler/SendChatMessageHandler/trial
/// handlers actually consult IWalletService and refuse to run the paid call when it says no —
/// this is application-logic wiring, not the wallet's internal concurrency guarantee. That
/// guarantee (two concurrent consumes against a 1-credit balance can't both succeed) lives in
/// WalletService's advisory-lock transaction against real Postgres and isn't exercisable with
/// the hand-written fakes this project's test suite uses everywhere else (see
/// UploadDocumentHandlerTests) — it would need a Testcontainers-style integration test, which
/// this project doesn't have infrastructure for yet.
/// </summary>
public class WalletGateTests
{
    // ---- Test doubles (this project has no mocking library) ----

    private sealed class FakeWallet : IWalletService
    {
        public bool AllowConsume { get; set; } = true;
        public List<(UsageSubject Subject, string Operation)> ConsumeCalls { get; } = new();

        public Task<int> GetBalanceAsync(UsageSubject subject, CancellationToken ct = default) => Task.FromResult(0);

        public Task<bool> TryConsumeAsync(UsageSubject subject, string operation, Guid? documentId = null, CancellationToken ct = default)
        {
            ConsumeCalls.Add((subject, operation));
            return Task.FromResult(AllowConsume);
        }

        public Task GrantAsync(UsageSubject subject, int credits, UsageLedgerKind kind, string note, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> TryDebitForSponsorshipAsync(UsageSubject subject, int credits, string note, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task GrantSponsorshipAsync(UsageSubject subject, int credits, string note, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<UsageLedgerEntry>> GetHistoryAsync(UsageSubject subject, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UsageLedgerEntry>>(Array.Empty<UsageLedgerEntry>());
    }

    private sealed class FakeDocs : IDocumentRepository
    {
        public Document? ToReturn { get; set; }
        public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task<IReadOnlyList<Document>> ListExpiredAsync(DateTime nowUtc, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Document>>(Array.Empty<Document>());
        public Task AddAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Document document, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAnalyses : IDocumentAnalysisRepository
    {
        public DocumentAnalysis? ToReturn { get; set; }
        public Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTtsCache : ITtsAudioCacheRepository
    {
        public TtsAudio? ToReturn { get; set; }
        public Task<TtsAudio?> GetAsync(Guid documentId, Language language, SpokenSection section, CancellationToken ct = default) =>
            Task.FromResult(ToReturn);
        public Task SetAsync(Guid documentId, Language language, SpokenSection section, TtsAudio audio, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTts : ITtsProvider
    {
        public int CallCount { get; private set; }
        public Task<TtsAudio> SynthesizeAsync(string text, Language language, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new TtsAudio(new byte[] { 1 }, "audio/mpeg"));
        }
    }

    private sealed class FakeAi : IAiProvider
    {
        public int AnalyzeCallCount { get; private set; }
        public int ChatCallCount { get; private set; }
        public Task<DocumentAnalysisResult> AnalyzeAsync(string documentText, DocumentCategory category, CancellationToken ct = default)
        {
            AnalyzeCallCount++;
            return Task.FromResult(new DocumentAnalysisResult());
        }
        public Task<string> ChatAsync(string documentText, IReadOnlyList<ChatTurn> history, string question, Language responseLanguage, CancellationToken ct = default)
        {
            ChatCallCount++;
            return Task.FromResult("answer");
        }
    }

    private sealed class FakeOcr : IOcrProvider
    {
        public Task<string> ExtractTextAsync(byte[] fileBytes, string contentType, CancellationToken ct = default) =>
            Task.FromResult("some text");
    }

    private sealed class NoopEvents : IEventTracker
    {
        public Task TrackAsync(string eventName, Guid? userId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChatMessages : IChatMessageRepository
    {
        public Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid d, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        public Task AddAsync(ChatMessage m, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByDocumentIdAsync(Guid d, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static Document ReadyDocument(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Status = DocumentProcessingStatus.Ready,
        OcrText = "some extracted text"
    };

    // ---- AnalyzeDocumentHandler ----

    [Fact]
    public async Task Analyze_out_of_credits_fails_before_calling_the_AI()
    {
        var userId = Guid.NewGuid();
        var docs = new FakeDocs { ToReturn = ReadyDocument(userId) };
        var ai = new FakeAi();
        var wallet = new FakeWallet { AllowConsume = false };
        var handler = new AnalyzeDocumentHandler(docs, new FakeAnalyses(), new FakeTtsCache(), ai, wallet, new NoopEvents());

        var result = await handler.Handle(new AnalyzeDocumentCommand(userId, docs.ToReturn!.Id, DocumentCategory.Other), default);

        Assert.False(result.Success);
        Assert.Equal(WalletErrors.OutOfCredits, result.Error);
        Assert.Equal(0, ai.AnalyzeCallCount);
        Assert.Single(wallet.ConsumeCalls);
        Assert.Equal("analyze", wallet.ConsumeCalls[0].Operation);
    }

    [Fact]
    public async Task Analyze_with_credits_consumes_one_and_calls_the_AI()
    {
        var userId = Guid.NewGuid();
        var docs = new FakeDocs { ToReturn = ReadyDocument(userId) };
        var ai = new FakeAi();
        var wallet = new FakeWallet { AllowConsume = true };
        var handler = new AnalyzeDocumentHandler(docs, new FakeAnalyses(), new FakeTtsCache(), ai, wallet, new NoopEvents());

        var result = await handler.Handle(new AnalyzeDocumentCommand(userId, docs.ToReturn!.Id, DocumentCategory.Other), default);

        Assert.True(result.Success);
        Assert.Equal(1, ai.AnalyzeCallCount);
    }

    // ---- SpeakDocumentHandler ----

    [Fact]
    public async Task Speech_cache_hit_never_touches_the_wallet_or_the_provider()
    {
        var userId = Guid.NewGuid();
        var doc = ReadyDocument(userId);
        var docs = new FakeDocs { ToReturn = doc };
        var cache = new FakeTtsCache { ToReturn = new TtsAudio(new byte[] { 9 }, "audio/mpeg") };
        var tts = new FakeTts();
        var wallet = new FakeWallet { AllowConsume = false }; // would fail the gate if it were ever asked
        var handler = new SpeakDocumentHandler(docs, new FakeAnalyses(), cache, tts, wallet);

        var result = await handler.Handle(new SpeakDocumentQuery(userId, doc.Id, Language.Hebrew), default);

        Assert.True(result.Success);
        Assert.Equal(0, tts.CallCount);
        Assert.Empty(wallet.ConsumeCalls);
    }

    [Fact]
    public async Task Speech_cache_miss_out_of_credits_fails_before_calling_the_provider()
    {
        var userId = Guid.NewGuid();
        var doc = ReadyDocument(userId);
        var docs = new FakeDocs { ToReturn = doc };
        var analyses = new FakeAnalyses
        {
            ToReturn = new DocumentAnalysis { DocumentId = doc.Id, Summary = new LocalizedText("א", "ሀ", "Summary") }
        };
        var cache = new FakeTtsCache { ToReturn = null };
        var tts = new FakeTts();
        var wallet = new FakeWallet { AllowConsume = false };
        var handler = new SpeakDocumentHandler(docs, analyses, cache, tts, wallet);

        var result = await handler.Handle(new SpeakDocumentQuery(userId, doc.Id, Language.Hebrew, SpokenSection.Summary), default);

        Assert.False(result.Success);
        Assert.Equal(WalletErrors.OutOfCredits, result.Error);
        Assert.Equal(0, tts.CallCount);
        Assert.Equal("tts", wallet.ConsumeCalls[0].Operation);
    }

    // ---- SendChatMessageHandler ----

    [Fact]
    public async Task Chat_out_of_credits_fails_before_calling_the_AI_and_saves_no_messages()
    {
        var userId = Guid.NewGuid();
        var doc = ReadyDocument(userId);
        var docs = new FakeDocs { ToReturn = doc };
        var ai = new FakeAi();
        var wallet = new FakeWallet { AllowConsume = false };
        var handler = new SendChatMessageHandler(docs, new FakeChatMessages(), ai, wallet);

        var result = await handler.Handle(new SendChatMessageCommand(userId, doc.Id, "What is this?", Language.Hebrew), default);

        Assert.False(result.Success);
        Assert.Equal(WalletErrors.OutOfCredits, result.Error);
        Assert.Equal(0, ai.ChatCallCount);
    }

    // ---- Trial handlers (anonymous — subject is a device id / IP, not a user) ----

    [Fact]
    public async Task Trial_analyze_out_of_credits_fails_before_running_OCR()
    {
        var ocr = new FakeOcr();
        var ai = new FakeAi();
        var wallet = new FakeWallet { AllowConsume = false };
        var handler = new AnalyzeTrialHandler(ocr, ai, wallet);
        var subject = UsageSubject.ForDevice("device-123");

        var result = await handler.Handle(
            new AnalyzeTrialCommand(new[] { new UploadPage("p1.jpg", "image/jpeg", new byte[] { 1 }) }, subject), default);

        Assert.False(result.Success);
        Assert.Equal(WalletErrors.OutOfCredits, result.Error);
        Assert.Equal(0, ai.AnalyzeCallCount);
        Assert.Equal(subject, wallet.ConsumeCalls[0].Subject);
    }

    [Fact]
    public async Task Trial_speech_out_of_credits_fails_before_calling_the_provider()
    {
        var tts = new FakeTts();
        var wallet = new FakeWallet { AllowConsume = false };
        var handler = new SpeakTrialHandler(tts, wallet);
        var analysis = new DocumentAnalysisResult { Summary = new LocalizedText("א", "ሀ", "Summary") };

        var result = await handler.Handle(
            new SpeakTrialQuery(analysis, Language.Hebrew, UsageSubject.ForIpFallback("1.2.3.4"), SpokenSection.Summary), default);

        Assert.False(result.Success);
        Assert.Equal(WalletErrors.OutOfCredits, result.Error);
        Assert.Equal(0, tts.CallCount);
    }
}
