namespace AmharicHelper.Application.DTOs;

/// <summary>How many times one funnel event fired since the query's cutoff.</summary>
public record EventCountDto(string Name, int Count);

/// <summary>The admin funnel view — event counts over the requested window.</summary>
public record FunnelDto(DateTime SinceUtc, IReadOnlyList<EventCountDto> Counts);

/// <summary>One occurrence of a funnel event, drilled down from the count — who (if the account
/// still exists) and when. Backs the click-through list behind each funnel bar.</summary>
public record EventDetailDto(string? Email, DateTime CreatedAt);
