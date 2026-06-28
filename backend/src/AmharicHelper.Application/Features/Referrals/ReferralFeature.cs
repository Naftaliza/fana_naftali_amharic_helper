using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Referrals;

/// <summary>Up to <paramref name="Limit"/> active providers matched to a document category.</summary>
public record GetProvidersQuery(DocumentCategory Category, int Limit = 3)
    : IRequest<Result<IReadOnlyList<ProviderDto>>>;

public class GetProvidersHandler(IProviderRepository providers)
    : IRequestHandler<GetProvidersQuery, Result<IReadOnlyList<ProviderDto>>>
{
    public async Task<Result<IReadOnlyList<ProviderDto>>> Handle(GetProvidersQuery q, CancellationToken ct)
    {
        var list = await providers.GetActiveByCategoryAsync(q.Category, q.Limit, ct);
        var dtos = (IReadOnlyList<ProviderDto>)list
            .Select(p => new ProviderDto(p.Id, (int)p.Category, p.DisplayName, p.Phone, p.WhatsApp, p.City, p.Blurb))
            .ToList();
        return Result<IReadOnlyList<ProviderDto>>.Ok(dtos);
    }
}

/// <summary>Records a user→provider contact — the billable referral unit.</summary>
public record LogLeadCommand(Guid ProviderId, DocumentCategory Category, UrgencyLevel Urgency, Guid? DocumentId, string? Ref)
    : IRequest<Result<bool>>;

public class LogLeadHandler(IProviderRepository providers, ILeadRepository leads)
    : IRequestHandler<LogLeadCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LogLeadCommand cmd, CancellationToken ct)
    {
        var provider = await providers.GetByIdAsync(cmd.ProviderId, ct);
        if (provider is null) return Result<bool>.Fail("Provider not found.");

        await leads.AddAsync(new Lead
        {
            ProviderId = cmd.ProviderId,
            Category = cmd.Category,
            Urgency = cmd.Urgency,
            DocumentId = cmd.DocumentId,
            Ref = cmd.Ref
        }, ct);
        return Result<bool>.Ok(true);
    }
}
