using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Legal;

// ---- Public: fetch the current Terms/Privacy body in one language ----
public record GetLegalDocumentQuery(string Kind, Language Language) : IRequest<Result<LegalDocumentDto>>;

public class GetLegalDocumentHandler(ILegalDocumentRepository legal)
    : IRequestHandler<GetLegalDocumentQuery, Result<LegalDocumentDto>>
{
    public async Task<Result<LegalDocumentDto>> Handle(GetLegalDocumentQuery q, CancellationToken ct)
    {
        var kind = q.Kind.Trim().ToLowerInvariant();
        if (kind is not ("terms" or "privacy"))
            return Result<LegalDocumentDto>.Fail("Unknown document kind.");

        var doc = await legal.GetLatestAsync(kind, ct);
        if (doc is null)
            return Result<LegalDocumentDto>.Fail("Document not found.");

        var body = new LocalizedText(doc.BodyHe, doc.BodyAm, doc.BodyEn).For(q.Language);
        return Result<LegalDocumentDto>.Ok(new LegalDocumentDto(doc.Kind, doc.Version, body, doc.EffectiveAt));
    }
}

// ---- Record that a subject (an account, or a hashed not-yet-registered contact) accepted the
// current version of a legal document. Idempotent in effect: recording the same acceptance twice
// just adds a second timestamped row, which is fine — it's a log, not a toggle. ----
public record RecordConsentCommand(Guid? UserId, string? ContactHash, string Kind, string? SourceIp)
    : IRequest<Result<bool>>;

public class RecordConsentHandler(ILegalDocumentRepository legal, IConsentRepository consents)
    : IRequestHandler<RecordConsentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RecordConsentCommand cmd, CancellationToken ct)
    {
        if (cmd.UserId is null && string.IsNullOrWhiteSpace(cmd.ContactHash))
            return Result<bool>.Fail("A subject (user or contact) is required.");

        var kind = cmd.Kind.Trim().ToLowerInvariant();
        var doc = await legal.GetLatestAsync(kind, ct);
        if (doc is null)
            return Result<bool>.Fail("Unknown document kind.");

        await consents.AddAsync(new ConsentRecord
        {
            UserId = cmd.UserId,
            ContactHash = cmd.ContactHash,
            Kind = kind,
            Version = doc.Version,
            SourceIp = cmd.SourceIp
        }, ct);
        return Result<bool>.Ok(true);
    }
}
