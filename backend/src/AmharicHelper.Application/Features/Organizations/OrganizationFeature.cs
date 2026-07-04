using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace AmharicHelper.Application.Features.Organizations;

// ---- Public: resolve branding by slug — drives the white-labeled front end ----
public record GetOrganizationBySlugQuery(string Slug) : IRequest<Result<OrganizationBrandingDto>>;

public class GetOrganizationBySlugHandler(IOrganizationRepository organizations)
    : IRequestHandler<GetOrganizationBySlugQuery, Result<OrganizationBrandingDto>>
{
    public async Task<Result<OrganizationBrandingDto>> Handle(GetOrganizationBySlugQuery q, CancellationToken ct)
    {
        var org = await organizations.GetBySlugAsync(q.Slug.Trim().ToLowerInvariant(), ct);
        if (org is null || !org.IsActive)
            return Result<OrganizationBrandingDto>.Fail("Organization not found.");

        return Result<OrganizationBrandingDto>.Ok(new OrganizationBrandingDto(
            org.Slug, org.Name, org.LogoUrl, org.PrimaryColorHex, org.AccentColorHex, org.WelcomeText));
    }
}

// ---- Admin (global superadmin): create a new tenant ----
// Self-serve tenant onboarding isn't built yet — early B2B/B2G deals are sales-assisted
// pilots, so only a global admin provisions a tenant for now.
public record CreateOrganizationCommand(Guid AdminUserId, CreateOrganizationRequest Request) : IRequest<Result<Guid>>;

public class CreateOrganizationHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<CreateOrganizationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrganizationCommand cmd, CancellationToken ct)
    {
        if (!await IsAdminAsync(users, config, cmd.AdminUserId, ct))
            return Result<Guid>.Fail("Forbidden");

        var r = cmd.Request;
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Slug))
            return Result<Guid>.Fail("Name and slug are required.");

        var slug = r.Slug.Trim().ToLowerInvariant();
        if (await organizations.GetBySlugAsync(slug, ct) is not null)
            return Result<Guid>.Fail("That slug is already taken.");

        var org = new Organization
        {
            Name = r.Name.Trim(),
            Slug = slug,
            LogoUrl = string.IsNullOrWhiteSpace(r.LogoUrl) ? null : r.LogoUrl.Trim(),
            PrimaryColorHex = string.IsNullOrWhiteSpace(r.PrimaryColorHex) ? "#2563EB" : r.PrimaryColorHex.Trim(),
            AccentColorHex = string.IsNullOrWhiteSpace(r.AccentColorHex) ? null : r.AccentColorHex.Trim(),
            // One welcome message supplied at creation; store in all three language slots so
            // the tenant landing renders in any UI language. An admin can refine it later.
            WelcomeText = new LocalizedText(r.WelcomeText, r.WelcomeText, r.WelcomeText)
        };
        await organizations.AddAsync(org, ct);
        return Result<Guid>.Ok(org.Id);
    }

    internal static async Task<bool> IsAdminAsync(
        IUserRepository users, IConfiguration config, Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        return AdminPolicy.IsAdmin(user?.Email, config["Admin:Emails"]);
    }
}

// ---- Admin: list tenants ----
public record ListOrganizationsQuery(Guid AdminUserId) : IRequest<Result<IReadOnlyList<OrganizationSummaryDto>>>;

public class ListOrganizationsHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<ListOrganizationsQuery, Result<IReadOnlyList<OrganizationSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<OrganizationSummaryDto>>> Handle(ListOrganizationsQuery q, CancellationToken ct)
    {
        if (!await CreateOrganizationHandler.IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<IReadOnlyList<OrganizationSummaryDto>>.Fail("Forbidden");

        var list = await organizations.ListAsync(ct);
        var dtos = (IReadOnlyList<OrganizationSummaryDto>)list
            .Select(o => new OrganizationSummaryDto(
                o.Id, o.Name, o.Slug, o.IsActive, o.CreatedAt, o.LogoUrl, o.PrimaryColorHex, o.AccentColorHex, o.WelcomeText.En))
            .ToList();
        return Result<IReadOnlyList<OrganizationSummaryDto>>.Ok(dtos);
    }
}

// ---- Admin: edit a tenant's branding (Slug is locked; see IOrganizationRepository) ----
public record UpdateOrganizationCommand(Guid AdminUserId, Guid OrganizationId, UpdateOrganizationRequest Request)
    : IRequest<Result<bool>>;

public class UpdateOrganizationHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<UpdateOrganizationCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateOrganizationCommand cmd, CancellationToken ct)
    {
        if (!await CreateOrganizationHandler.IsAdminAsync(users, config, cmd.AdminUserId, ct))
            return Result<bool>.Fail("Forbidden");

        var org = await organizations.GetByIdAsync(cmd.OrganizationId, ct);
        if (org is null) return Result<bool>.Fail("Organization not found.");

        var r = cmd.Request;
        if (string.IsNullOrWhiteSpace(r.Name))
            return Result<bool>.Fail("Name is required.");

        org.Name = r.Name.Trim();
        org.LogoUrl = string.IsNullOrWhiteSpace(r.LogoUrl) ? null : r.LogoUrl.Trim();
        org.PrimaryColorHex = string.IsNullOrWhiteSpace(r.PrimaryColorHex) ? org.PrimaryColorHex : r.PrimaryColorHex.Trim();
        org.AccentColorHex = string.IsNullOrWhiteSpace(r.AccentColorHex) ? null : r.AccentColorHex.Trim();
        org.WelcomeText = new LocalizedText(r.WelcomeText, r.WelcomeText, r.WelcomeText);

        await organizations.UpdateAsync(org, ct);
        return Result<bool>.Ok(true);
    }
}

// ---- Admin: activate/deactivate a tenant ----
public record SetOrganizationActiveCommand(Guid AdminUserId, Guid OrganizationId, bool Active)
    : IRequest<Result<bool>>;

public class SetOrganizationActiveHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<SetOrganizationActiveCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetOrganizationActiveCommand cmd, CancellationToken ct)
    {
        if (!await CreateOrganizationHandler.IsAdminAsync(users, config, cmd.AdminUserId, ct))
            return Result<bool>.Fail("Forbidden");

        if (await organizations.GetByIdAsync(cmd.OrganizationId, ct) is null)
            return Result<bool>.Fail("Organization not found.");

        await organizations.SetActiveAsync(cmd.OrganizationId, cmd.Active, ct);
        return Result<bool>.Ok(true);
    }
}

// ---- Admin: usage stats for one tenant (the renewal-justification dashboard) ----
public record GetOrganizationStatsQuery(Guid AdminUserId, Guid OrganizationId) : IRequest<Result<OrganizationStatsDto>>;

public class GetOrganizationStatsHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<GetOrganizationStatsQuery, Result<OrganizationStatsDto>>
{
    public async Task<Result<OrganizationStatsDto>> Handle(GetOrganizationStatsQuery q, CancellationToken ct)
    {
        if (!await CreateOrganizationHandler.IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<OrganizationStatsDto>.Fail("Forbidden");

        if (await organizations.GetByIdAsync(q.OrganizationId, ct) is null)
            return Result<OrganizationStatsDto>.Fail("Organization not found.");

        return Result<OrganizationStatsDto>.Ok(await organizations.GetStatsAsync(q.OrganizationId, ct));
    }
}
