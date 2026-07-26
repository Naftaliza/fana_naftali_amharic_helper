using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Analytics;
using AmharicHelper.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AmharicHelper.UnitTests;

public class AnalyticsFeatureTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeAnalyticsEventRepository : IAnalyticsEventRepository
    {
        public List<AnalyticsEvent> Added { get; } = new();
        public DateTime? LastSinceUtc { get; private set; }
        public string? LastDetailName { get; private set; }
        public IReadOnlyList<EventCountDto> ToReturn { get; set; } = Array.Empty<EventCountDto>();
        public IReadOnlyList<EventDetailDto> DetailsToReturn { get; set; } = Array.Empty<EventDetailDto>();

        public Task AddAsync(AnalyticsEvent evt, CancellationToken ct = default) { Added.Add(evt); return Task.CompletedTask; }

        public Task<IReadOnlyList<EventCountDto>> GetFunnelCountsAsync(DateTime sinceUtc, CancellationToken ct = default)
        {
            LastSinceUtc = sinceUtc;
            return Task.FromResult(ToReturn);
        }

        public Task<IReadOnlyList<EventDetailDto>> GetEventDetailsAsync(string name, DateTime sinceUtc, CancellationToken ct = default)
        {
            LastDetailName = name;
            LastSinceUtc = sinceUtc;
            return Task.FromResult(DetailsToReturn);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ToReturn { get; set; }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static IConfiguration Config(string adminEmails = "admin@test.local") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Emails"] = adminEmails }).Build();

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid NonAdminUserId = Guid.NewGuid();

    private static FakeUserRepository AdminUsers() => new()
    {
        ToReturn = new User { Id = AdminUserId, Email = "admin@test.local" }
    };

    [Fact]
    public async Task Non_admin_is_forbidden()
    {
        var events = new FakeAnalyticsEventRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new GetFunnelHandler(events, users, Config());

        var result = await handler.Handle(new GetFunnelQuery(NonAdminUserId, 30), default);

        Assert.False(result.Success);
        Assert.Null(events.LastSinceUtc); // never even queried
    }

    [Fact]
    public async Task Admin_gets_the_funnel_counts()
    {
        var events = new FakeAnalyticsEventRepository
        {
            ToReturn = new[] { new EventCountDto("user_registered", 5), new EventCountDto("document_uploaded", 3) }
        };
        var handler = new GetFunnelHandler(events, AdminUsers(), Config());

        var result = await handler.Handle(new GetFunnelQuery(AdminUserId, 30), default);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Counts.Count);
        Assert.Contains(result.Value.Counts, c => c.Name == "user_registered" && c.Count == 5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000)]
    public async Task Out_of_range_days_falls_back_to_30(int days)
    {
        var events = new FakeAnalyticsEventRepository();
        var handler = new GetFunnelHandler(events, AdminUsers(), Config());
        var before = DateTime.UtcNow;

        await handler.Handle(new GetFunnelQuery(AdminUserId, days), default);

        var expected = before.AddDays(-30);
        Assert.True(events.LastSinceUtc.HasValue);
        Assert.True(Math.Abs((events.LastSinceUtc!.Value - expected).TotalMinutes) < 1);
    }

    [Fact]
    public async Task Details_non_admin_is_forbidden()
    {
        var events = new FakeAnalyticsEventRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new GetFunnelEventDetailsHandler(events, users, Config());

        var result = await handler.Handle(new GetFunnelEventDetailsQuery(NonAdminUserId, "user_registered", 30), default);

        Assert.False(result.Success);
        Assert.Null(events.LastDetailName); // never even queried
    }

    [Fact]
    public async Task Details_rejects_unknown_event_name()
    {
        var events = new FakeAnalyticsEventRepository();
        var handler = new GetFunnelEventDetailsHandler(events, AdminUsers(), Config());

        var result = await handler.Handle(new GetFunnelEventDetailsQuery(AdminUserId, "not_a_real_event", 30), default);

        Assert.False(result.Success);
        Assert.Equal("Unknown event name", result.Error);
        Assert.Null(events.LastDetailName);
    }

    [Fact]
    public async Task Admin_gets_the_event_details()
    {
        var events = new FakeAnalyticsEventRepository
        {
            DetailsToReturn = new[] { new EventDetailDto("a@test.local", DateTime.UtcNow), new EventDetailDto(null, DateTime.UtcNow) },
        };
        var handler = new GetFunnelEventDetailsHandler(events, AdminUsers(), Config());

        var result = await handler.Handle(new GetFunnelEventDetailsQuery(AdminUserId, "user_registered", 30), default);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("user_registered", events.LastDetailName);
    }
}
