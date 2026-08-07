using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Features.Legal;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class LegalFeatureTests
{
    private sealed class FakeLegal : ILegalDocumentRepository
    {
        public LegalDocument? ToReturn { get; set; }
        public Task<LegalDocument?> GetLatestAsync(string kind, CancellationToken ct = default) => Task.FromResult(ToReturn);
    }

    private sealed class FakeConsents : IConsentRepository
    {
        public ConsentRecord? Added { get; private set; }
        public Task AddAsync(ConsentRecord record, CancellationToken ct = default) { Added = record; return Task.CompletedTask; }
    }

    // ---- GetLegalDocumentHandler ----

    [Fact]
    public async Task Unknown_kind_fails()
    {
        var handler = new GetLegalDocumentHandler(new FakeLegal());
        var result = await handler.Handle(new GetLegalDocumentQuery("refund", Language.Hebrew), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task No_seeded_document_fails()
    {
        var legal = new FakeLegal { ToReturn = null };
        var handler = new GetLegalDocumentHandler(legal);
        var result = await handler.Handle(new GetLegalDocumentQuery("terms", Language.Hebrew), default);
        Assert.False(result.Success);
        Assert.Equal("Document not found.", result.Error);
    }

    [Fact]
    public async Task Returns_the_body_in_the_requested_language()
    {
        var legal = new FakeLegal
        {
            ToReturn = new LegalDocument { Kind = "privacy", Version = 3, BodyHe = "עברית", BodyAm = "አማርኛ", BodyEn = "English" }
        };
        var handler = new GetLegalDocumentHandler(legal);

        var result = await handler.Handle(new GetLegalDocumentQuery("PRIVACY", Language.Amharic), default);

        Assert.True(result.Success);
        Assert.Equal("privacy", result.Value!.Kind);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal("አማርኛ", result.Value.Body);
    }

    // ---- RecordConsentHandler ----

    [Fact]
    public async Task Consent_with_no_subject_fails()
    {
        var handler = new RecordConsentHandler(new FakeLegal(), new FakeConsents());
        var result = await handler.Handle(new RecordConsentCommand(null, null, "terms", "1.2.3.4"), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Consent_for_unknown_kind_fails()
    {
        var handler = new RecordConsentHandler(new FakeLegal { ToReturn = null }, new FakeConsents());
        var result = await handler.Handle(new RecordConsentCommand(Guid.NewGuid(), null, "terms", null), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Consent_records_the_current_version_for_a_hashed_contact()
    {
        var legal = new FakeLegal { ToReturn = new LegalDocument { Kind = "terms", Version = 2 } };
        var consents = new FakeConsents();
        var handler = new RecordConsentHandler(legal, consents);

        var result = await handler.Handle(new RecordConsentCommand(null, "abc123hash", "terms", "1.2.3.4"), default);

        Assert.True(result.Success);
        Assert.Equal("abc123hash", consents.Added!.ContactHash);
        Assert.Equal(2, consents.Added.Version);
        Assert.Equal("1.2.3.4", consents.Added.SourceIp);
    }
}
