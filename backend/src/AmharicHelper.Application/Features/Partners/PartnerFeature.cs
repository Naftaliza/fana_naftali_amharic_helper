using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Application.Features.Partners;

// ---- Public: a business applies to be listed ----
public record ApplyAsProviderCommand(ProviderApplicationRequest Request) : IRequest<Result<bool>>;

public class ApplyAsProviderHandler(
    IProviderRepository providers,
    ILogger<ApplyAsProviderHandler> logger) : IRequestHandler<ApplyAsProviderCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ApplyAsProviderCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        if (string.IsNullOrWhiteSpace(r.DisplayName) || string.IsNullOrWhiteSpace(r.ContactEmail))
            return Result<bool>.Fail("Business name and contact email are required.");

        // Business supplies one description; store it in all three language slots so the listing
        // renders in any UI language. An admin can refine the translations later.
        var blurb = new LocalizedText(r.Description, r.Description, r.Description);

        await providers.AddAsync(new Provider
        {
            Category = (DocumentCategory)r.Category,
            DisplayName = r.DisplayName.Trim(),
            City = r.City?.Trim(),
            Phone = r.Phone?.Trim(),
            WhatsApp = r.WhatsApp?.Trim(),
            ContactEmail = r.ContactEmail.Trim(),
            Blurb = blurb,
            IsActive = false,   // pending review — never shown to users until approved
            Priority = 0
        }, ct);

        // Visible in API logs too, so signups aren't missed even without the admin page open.
        logger.LogInformation("New provider application: {Name} <{Email}> (category {Category})",
            r.DisplayName, r.ContactEmail, r.Category);
        return Result<bool>.Ok(true);
    }
}

// ---- Admin: review queue ----
public record ListPendingProvidersQuery(Guid AdminUserId) : IRequest<Result<IReadOnlyList<PendingProviderDto>>>;

public class ListPendingProvidersHandler(
    IProviderRepository providers,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<ListPendingProvidersQuery, Result<IReadOnlyList<PendingProviderDto>>>
{
    public async Task<Result<IReadOnlyList<PendingProviderDto>>> Handle(ListPendingProvidersQuery q, CancellationToken ct)
    {
        if (!await IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<IReadOnlyList<PendingProviderDto>>.Fail("Forbidden");

        var pending = await providers.GetPendingAsync(ct);
        var dtos = (IReadOnlyList<PendingProviderDto>)pending
            .Select(p => new PendingProviderDto(
                p.Id, (int)p.Category, p.DisplayName, p.City, p.Phone, p.WhatsApp, p.ContactEmail, p.Blurb, p.CreatedAt))
            .ToList();
        return Result<IReadOnlyList<PendingProviderDto>>.Ok(dtos);
    }

    internal static async Task<bool> IsAdminAsync(
        IUserRepository users, IConfiguration config, Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        return AdminPolicy.IsAdmin(user?.Email, config["Admin:Emails"]);
    }
}

// ---- Admin: approve ----
public record ApproveProviderCommand(Guid AdminUserId, Guid ProviderId) : IRequest<Result<bool>>;

public class ApproveProviderHandler(
    IProviderRepository providers,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<ApproveProviderCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ApproveProviderCommand cmd, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, cmd.AdminUserId, ct))
            return Result<bool>.Fail("Forbidden");
        await providers.SetActiveAsync(cmd.ProviderId, true, ct);
        return Result<bool>.Ok(true);
    }
}

// ---- Admin: reject (delete) ----
public record RejectProviderCommand(Guid AdminUserId, Guid ProviderId) : IRequest<Result<bool>>;

public class RejectProviderHandler(
    IProviderRepository providers,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<RejectProviderCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RejectProviderCommand cmd, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, cmd.AdminUserId, ct))
            return Result<bool>.Fail("Forbidden");
        await providers.DeleteAsync(cmd.ProviderId, ct);
        return Result<bool>.Ok(true);
    }
}
