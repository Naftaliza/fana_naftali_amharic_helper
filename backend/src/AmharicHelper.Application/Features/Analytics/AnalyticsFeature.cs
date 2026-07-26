using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Partners;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace AmharicHelper.Application.Features.Analytics;

/// <summary>The admin funnel view: event counts (registered/verified/uploaded/analyzed) over a
/// trailing window — the minimal signal the app had none of before (see the plan). Admin-gated
/// the same way every other admin query is (ListPendingProvidersHandler.IsAdminAsync).</summary>
public record GetFunnelQuery(Guid AdminUserId, int Days) : IRequest<Result<FunnelDto>>;

public class GetFunnelHandler(
    IAnalyticsEventRepository events,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<GetFunnelQuery, Result<FunnelDto>>
{
    public async Task<Result<FunnelDto>> Handle(GetFunnelQuery q, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<FunnelDto>.Fail("Forbidden");

        var days = q.Days is > 0 and <= 365 ? q.Days : 30;
        var since = DateTime.UtcNow.AddDays(-days);
        var counts = await events.GetFunnelCountsAsync(since, ct);
        return Result<FunnelDto>.Ok(new FunnelDto(since, counts));
    }
}

/// <summary>The click-through behind one funnel bar — who fired a given event and when, newest
/// first. Same admin gate as GetFunnelQuery.</summary>
public record GetFunnelEventDetailsQuery(Guid AdminUserId, string EventName, int Days) : IRequest<Result<IReadOnlyList<EventDetailDto>>>;

public class GetFunnelEventDetailsHandler(
    IAnalyticsEventRepository events,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<GetFunnelEventDetailsQuery, Result<IReadOnlyList<EventDetailDto>>>
{
    public async Task<Result<IReadOnlyList<EventDetailDto>>> Handle(GetFunnelEventDetailsQuery q, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<IReadOnlyList<EventDetailDto>>.Fail("Forbidden");

        if (!EventNames.All.Contains(q.EventName))
            return Result<IReadOnlyList<EventDetailDto>>.Fail("Unknown event name");

        var days = q.Days is > 0 and <= 365 ? q.Days : 30;
        var since = DateTime.UtcNow.AddDays(-days);
        var details = await events.GetEventDetailsAsync(q.EventName, since, ct);
        return Result<IReadOnlyList<EventDetailDto>>.Ok(details);
    }
}
