using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Application.Common;

/// <summary>Default IEventTracker — persists via IAnalyticsEventRepository. A tracking failure is
/// logged and swallowed, never rethrown: recording a funnel event must never break the
/// user-facing operation it's attached to (registration, upload, analysis).</summary>
public class EventTracker(IAnalyticsEventRepository events, ILogger<EventTracker> logger) : IEventTracker
{
    public async Task TrackAsync(string eventName, Guid? userId = null, CancellationToken ct = default)
    {
        try
        {
            await events.AddAsync(new AnalyticsEvent { Name = eventName, UserId = userId }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record analytics event {Event}", eventName);
        }
    }
}
