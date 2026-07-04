using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Organizations;
using AmharicHelper.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AmharicHelper.UnitTests;

public class OrganizationFeatureTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public Organization? ToReturnBySlug { get; set; }
        public Organization? ToReturnById { get; set; }
        public Organization? Added { get; private set; }
        public Organization? Updated { get; private set; }
        public (Guid Id, bool Active)? SetActiveCall { get; private set; }

        public Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(ToReturnBySlug);
        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturnById);
        public Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Organization>>(Array.Empty<Organization>());
        public Task AddAsync(Organization organization, CancellationToken ct = default) { Added = organization; return Task.CompletedTask; }
        public Task UpdateAsync(Organization organization, CancellationToken ct = default) { Updated = organization; return Task.CompletedTask; }
        public Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default) { SetActiveCall = (id, active); return Task.CompletedTask; }
        public Task<OrganizationStatsDto> GetStatsAsync(Guid organizationId, CancellationToken ct = default) =>
            Task.FromResult(new OrganizationStatsDto(0, 0, 0, Array.Empty<CategoryCountDto>(), Array.Empty<UrgencyCountDto>(), Array.Empty<WeeklyCountDto>()));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ToReturn { get; set; }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static IConfiguration Config(string adminEmails = "admin@test.local") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Emails"] = adminEmails }).Build();

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid NonAdminUserId = Guid.NewGuid();

    private static FakeUserRepository Users() => new()
    {
        ToReturn = new User { Id = AdminUserId, Email = "admin@test.local" }
    };

    // ---- CreateOrganizationHandler ----

    [Fact]
    public async Task Create_by_non_admin_is_forbidden()
    {
        var orgs = new FakeOrganizationRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new CreateOrganizationHandler(orgs, users, Config());

        var result = await handler.Handle(new CreateOrganizationCommand(NonAdminUserId,
            new CreateOrganizationRequest("Acme", "acme", null, "#2563EB", null, "Welcome")), default);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
        Assert.Null(orgs.Added);
    }

    [Fact]
    public async Task Create_with_taken_slug_fails()
    {
        var orgs = new FakeOrganizationRepository { ToReturnBySlug = new Organization { Slug = "acme" } };
        var handler = new CreateOrganizationHandler(orgs, Users(), Config());

        var result = await handler.Handle(new CreateOrganizationCommand(AdminUserId,
            new CreateOrganizationRequest("Acme", "ACME", null, "#2563EB", null, "Welcome")), default);

        Assert.False(result.Success);
        Assert.Equal("That slug is already taken.", result.Error);
        Assert.Null(orgs.Added);
    }

    [Fact]
    public async Task Create_lowercases_slug_and_fills_all_three_welcome_slots()
    {
        var orgs = new FakeOrganizationRepository();
        var handler = new CreateOrganizationHandler(orgs, Users(), Config());

        var result = await handler.Handle(new CreateOrganizationCommand(AdminUserId,
            new CreateOrganizationRequest("Acme", "  ACME  ", null, "#2563EB", null, "Welcome!")), default);

        Assert.True(result.Success);
        Assert.Equal("acme", orgs.Added!.Slug);
        Assert.Equal("Welcome!", orgs.Added.WelcomeText.He);
        Assert.Equal("Welcome!", orgs.Added.WelcomeText.Am);
        Assert.Equal("Welcome!", orgs.Added.WelcomeText.En);
    }

    // ---- UpdateOrganizationHandler ----

    [Fact]
    public async Task Update_by_non_admin_is_forbidden()
    {
        var orgs = new FakeOrganizationRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new UpdateOrganizationHandler(orgs, users, Config());

        var result = await handler.Handle(new UpdateOrganizationCommand(NonAdminUserId, Guid.NewGuid(),
            new UpdateOrganizationRequest("New Name", null, "#000000", null, "Hi")), default);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
    }

    [Fact]
    public async Task Update_unknown_organization_fails()
    {
        var orgs = new FakeOrganizationRepository { ToReturnById = null };
        var handler = new UpdateOrganizationHandler(orgs, Users(), Config());

        var result = await handler.Handle(new UpdateOrganizationCommand(AdminUserId, Guid.NewGuid(),
            new UpdateOrganizationRequest("New Name", null, "#000000", null, "Hi")), default);

        Assert.False(result.Success);
        Assert.Equal("Organization not found.", result.Error);
    }

    [Fact]
    public async Task Update_never_touches_slug()
    {
        var org = new Organization { Id = Guid.NewGuid(), Slug = "original-slug", Name = "Old Name" };
        var orgs = new FakeOrganizationRepository { ToReturnById = org };
        var handler = new UpdateOrganizationHandler(orgs, Users(), Config());

        var result = await handler.Handle(new UpdateOrganizationCommand(AdminUserId, org.Id,
            new UpdateOrganizationRequest("New Name", null, "#000000", null, "Hi")), default);

        Assert.True(result.Success);
        Assert.Equal("New Name", orgs.Updated!.Name);
        // The single most important regression to lock in: Slug is never part of the update path.
        Assert.Equal("original-slug", orgs.Updated.Slug);
    }

    // ---- SetOrganizationActiveHandler ----

    [Fact]
    public async Task SetActive_by_non_admin_is_forbidden()
    {
        var orgs = new FakeOrganizationRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new SetOrganizationActiveHandler(orgs, users, Config());

        var result = await handler.Handle(new SetOrganizationActiveCommand(NonAdminUserId, Guid.NewGuid(), false), default);

        Assert.False(result.Success);
        Assert.Null(orgs.SetActiveCall);
    }

    [Fact]
    public async Task SetActive_passes_through_exact_id_and_value()
    {
        var id = Guid.NewGuid();
        var orgs = new FakeOrganizationRepository { ToReturnById = new Organization { Id = id } };
        var handler = new SetOrganizationActiveHandler(orgs, Users(), Config());

        var result = await handler.Handle(new SetOrganizationActiveCommand(AdminUserId, id, false), default);

        Assert.True(result.Success);
        Assert.Equal((id, false), orgs.SetActiveCall);
    }

    // ---- GetOrganizationBySlugHandler ----

    [Fact]
    public async Task GetBySlug_hides_inactive_organization()
    {
        var orgs = new FakeOrganizationRepository
        {
            ToReturnBySlug = new Organization { Slug = "acme", Name = "Acme", IsActive = false }
        };
        var handler = new GetOrganizationBySlugHandler(orgs);

        var result = await handler.Handle(new GetOrganizationBySlugQuery("acme"), default);

        Assert.False(result.Success);
        Assert.Equal("Organization not found.", result.Error);
    }

    [Fact]
    public async Task GetBySlug_returns_branding_for_active_organization()
    {
        var org = new Organization { Slug = "acme", Name = "Acme", IsActive = true, PrimaryColorHex = "#111111" };
        var orgs = new FakeOrganizationRepository { ToReturnBySlug = org };
        var handler = new GetOrganizationBySlugHandler(orgs);

        var result = await handler.Handle(new GetOrganizationBySlugQuery("acme"), default);

        Assert.True(result.Success);
        Assert.Equal("Acme", result.Value!.Name);
        Assert.Equal("#111111", result.Value.PrimaryColorHex);
    }

    // ---- List / Stats: admin gate only ----

    [Fact]
    public async Task List_by_non_admin_is_forbidden()
    {
        var orgs = new FakeOrganizationRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new ListOrganizationsHandler(orgs, users, Config());

        var result = await handler.Handle(new ListOrganizationsQuery(NonAdminUserId), default);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
    }

    [Fact]
    public async Task Stats_by_non_admin_is_forbidden()
    {
        var orgs = new FakeOrganizationRepository();
        var users = new FakeUserRepository { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new GetOrganizationStatsHandler(orgs, users, Config());

        var result = await handler.Handle(new GetOrganizationStatsQuery(NonAdminUserId, Guid.NewGuid()), default);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
    }
}
