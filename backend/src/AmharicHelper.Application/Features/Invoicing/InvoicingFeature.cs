using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Partners;
using AmharicHelper.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Application.Features.Invoicing;

// ---- Admin: generate + send an invoice for one provider+month ----
public record GenerateAndSendInvoiceCommand(Guid AdminUserId, Guid ProviderId, int Year, int Month)
    : IRequest<Result<InvoiceDto>>;

public class GenerateAndSendInvoiceHandler(
    IProviderRepository providers,
    ILeadRepository leads,
    IInvoiceRepository invoices,
    IInvoicePdfBuilder pdfBuilder,
    IEmailSender emailSender,
    IUserRepository users,
    IConfiguration config,
    ILogger<GenerateAndSendInvoiceHandler> logger) : IRequestHandler<GenerateAndSendInvoiceCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(GenerateAndSendInvoiceCommand cmd, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, cmd.AdminUserId, ct))
            return Result<InvoiceDto>.Fail("Forbidden");

        var provider = await providers.GetByIdAsync(cmd.ProviderId, ct);
        if (provider is null) return Result<InvoiceDto>.Fail("Provider not found.");
        if (string.IsNullOrWhiteSpace(provider.ContactEmail))
            return Result<InvoiceDto>.Fail("This provider has no contact email on file. Add one on the Manage tab before invoicing.");

        var existing = await invoices.GetByProviderAndPeriodAsync(cmd.ProviderId, cmd.Year, cmd.Month, ct);
        if (existing is not null)
            return Result<InvoiceDto>.Fail($"An invoice for {cmd.Year}-{cmd.Month:D2} already exists ({existing.InvoiceNumber}).");

        var convertedLeads = await leads.GetConvertedForPeriodAsync(cmd.ProviderId, cmd.Year, cmd.Month, ct);
        if (convertedLeads.Count == 0)
            return Result<InvoiceDto>.Fail("No converted leads in this period — nothing to invoice.");

        var lineItems = convertedLeads
            .Select(l => new InvoiceLineItem(l.Id, l.Ref, l.Category, l.CreatedAt, provider.PricePerLead))
            .ToList();

        var invoice = new Invoice
        {
            ProviderId = provider.Id,
            InvoiceNumber = $"INV-{cmd.Year}-{cmd.Month:D2}-{provider.Id.ToString()[..8]}",
            PeriodYear = cmd.Year,
            PeriodMonth = cmd.Month,
            PricePerLead = provider.PricePerLead,
            Currency = "ILS",
            TotalAmount = lineItems.Count * provider.PricePerLead,
            LeadCount = lineItems.Count,
            LineItems = lineItems,
            Status = InvoiceStatus.Draft,
            SentToEmail = provider.ContactEmail!,
        };

        // The pre-check above is the primary idempotency guard, same convention as
        // CreateOrganizationHandler's slug pre-check — a genuine simultaneous-click race is left
        // to the DB's unique index + the global ExceptionHandlingMiddleware (500), not caught
        // here, matching how this codebase already handles this class of race elsewhere.
        await invoices.AddAsync(invoice, ct);

        var pdfBytes = pdfBuilder.Build(invoice, provider);

        try
        {
            await emailSender.SendAsync(new EmailMessage(
                invoice.SentToEmail,
                $"Invoice {invoice.InvoiceNumber} — {cmd.Year}-{cmd.Month:D2}",
                $"Hi {provider.DisplayName}, please find attached your invoice for {cmd.Year}-{cmd.Month:D2} " +
                $"({invoice.LeadCount} converted lead(s), total {invoice.TotalAmount} {invoice.Currency}). Thank you.",
                new[] { new EmailAttachment($"{invoice.InvoiceNumber}.pdf", pdfBytes, "application/pdf") }), ct);

            await invoices.MarkSentAsync(invoice.Id, DateTime.UtcNow, ct);
            invoice.Status = InvoiceStatus.Sent;
            invoice.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to email invoice {InvoiceNumber}", invoice.InvoiceNumber);
            await invoices.MarkFailedAsync(invoice.Id, ex.Message, ct);
            invoice.Status = InvoiceStatus.Failed;
            invoice.SendError = ex.Message;
        }

        return Result<InvoiceDto>.Ok(ToDto(invoice));
    }

    internal static InvoiceDto ToDto(Invoice i) => new(
        i.Id, i.ProviderId, i.InvoiceNumber, i.PeriodYear, i.PeriodMonth, i.Currency,
        i.TotalAmount, i.LeadCount, (int)i.Status, i.SentToEmail, i.GeneratedAt, i.SentAt, i.SendError);
}

// ---- Admin: check invoice status for a provider+month (drives the button/label in the UI) ----
public record GetInvoiceStatusQuery(Guid AdminUserId, Guid ProviderId, int Year, int Month)
    : IRequest<Result<InvoiceDto?>>;

public class GetInvoiceStatusHandler(
    IInvoiceRepository invoices,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<GetInvoiceStatusQuery, Result<InvoiceDto?>>
{
    public async Task<Result<InvoiceDto?>> Handle(GetInvoiceStatusQuery q, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<InvoiceDto?>.Fail("Forbidden");
        var inv = await invoices.GetByProviderAndPeriodAsync(q.ProviderId, q.Year, q.Month, ct);
        return Result<InvoiceDto?>.Ok(inv is null ? null : GenerateAndSendInvoiceHandler.ToDto(inv));
    }
}

// ---- Admin: (re-)download the PDF (for the "view PDF" affordance) ----
public record GetInvoicePdfQuery(Guid AdminUserId, Guid InvoiceId) : IRequest<Result<byte[]>>;

public class GetInvoicePdfHandler(
    IInvoiceRepository invoices,
    IProviderRepository providers,
    IInvoicePdfBuilder pdfBuilder,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<GetInvoicePdfQuery, Result<byte[]>>
{
    public async Task<Result<byte[]>> Handle(GetInvoicePdfQuery q, CancellationToken ct)
    {
        if (!await ListPendingProvidersHandler.IsAdminAsync(users, config, q.AdminUserId, ct))
            return Result<byte[]>.Fail("Forbidden");
        var invoice = await invoices.GetByIdAsync(q.InvoiceId, ct);
        if (invoice is null) return Result<byte[]>.Fail("Invoice not found.");
        var provider = await providers.GetByIdAsync(invoice.ProviderId, ct);
        if (provider is null) return Result<byte[]>.Fail("Provider not found.");
        return Result<byte[]>.Ok(pdfBuilder.Build(invoice, provider));
    }
}
