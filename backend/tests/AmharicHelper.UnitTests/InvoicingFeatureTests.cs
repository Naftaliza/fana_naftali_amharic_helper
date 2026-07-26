using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Invoicing;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AmharicHelper.UnitTests;

public class InvoicingFeatureTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeProviderRepository : IProviderRepository
    {
        public Provider? ToReturn { get; set; }
        public Task<IReadOnlyList<Provider>> GetActiveByCategoryAsync(DocumentCategory category, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Provider>>(Array.Empty<Provider>());
        public Task<Provider?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(Provider provider, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Provider>> GetPendingAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Provider>>(Array.Empty<Provider>());
        public Task<IReadOnlyList<Provider>> GetManagedAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Provider>>(Array.Empty<Provider>());
        public Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Provider provider, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        public IReadOnlyList<Lead> ConvertedToReturn { get; set; } = Array.Empty<Lead>();
        public Task AddAsync(Lead lead, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LeadSummaryDto>> GetSummaryAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LeadSummaryDto>>(Array.Empty<LeadSummaryDto>());
        public Task<IReadOnlyList<RecentLeadDto>> GetRecentAsync(int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RecentLeadDto>>(Array.Empty<RecentLeadDto>());
        public Task UpdateStatusAsync(Guid leadId, LeadStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> SetFeedbackAsync(string refCode, bool helpful, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Lead>> GetConvertedForPeriodAsync(Guid providerId, int year, int month, CancellationToken ct = default) =>
            Task.FromResult(ConvertedToReturn);
    }

    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public Invoice? ExistingForPeriod { get; set; }
        public Invoice? ToReturnById { get; set; }
        public int AddCount { get; private set; }
        public Invoice? Added { get; private set; }
        public Guid? MarkedSentId { get; private set; }
        public (Guid Id, string Error)? MarkedFailed { get; private set; }

        public Task<Invoice?> GetByProviderAndPeriodAsync(Guid providerId, int year, int month, CancellationToken ct = default) => Task.FromResult(ExistingForPeriod);
        public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturnById);
        public Task<IReadOnlyList<Invoice>> ListByProviderAsync(Guid providerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Invoice>>(Array.Empty<Invoice>());
        public Task AddAsync(Invoice invoice, CancellationToken ct = default) { AddCount++; Added = invoice; return Task.CompletedTask; }
        public Task MarkSentAsync(Guid id, DateTime sentAt, CancellationToken ct = default) { MarkedSentId = id; return Task.CompletedTask; }
        public Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default) { MarkedFailed = (id, error); return Task.CompletedTask; }
    }

    private sealed class FakeInvoicePdfBuilder : IInvoicePdfBuilder
    {
        public Invoice? LastInvoice { get; private set; }
        public Provider? LastProvider { get; private set; }
        public byte[] Build(Invoice invoice, Provider provider)
        {
            LastInvoice = invoice;
            LastProvider = provider;
            return new byte[] { 1, 2, 3 };
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public bool ThrowOnSend { get; set; }
        public List<EmailMessage> Sent { get; } = new();
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (ThrowOnSend) throw new InvalidOperationException("SMTP connection failed.");
            Sent.Add(message);
            return Task.CompletedTask;
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

    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Emails"] = "admin@test.local" }).Build();

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid NonAdminUserId = Guid.NewGuid();

    private static FakeUserRepository AdminUsers() => new() { ToReturn = new User { Id = AdminUserId, Email = "admin@test.local" } };
    private static FakeUserRepository NonAdminUsers() => new() { ToReturn = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };

    private static Provider MakeProvider(string? contactEmail = "provider@test.local", decimal pricePerLead = 50m) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Test Provider",
        ContactEmail = contactEmail,
        PricePerLead = pricePerLead,
    };

    private static Lead MakeConvertedLead(Guid providerId) => new()
    {
        Id = Guid.NewGuid(),
        ProviderId = providerId,
        Ref = "TEST-1",
        Status = LeadStatus.Converted,
        CreatedAt = DateTime.UtcNow,
    };

    private GenerateAndSendInvoiceHandler MakeHandler(
        FakeProviderRepository providers, FakeLeadRepository leads, FakeInvoiceRepository invoices,
        FakeInvoicePdfBuilder pdfBuilder, FakeEmailSender emailSender, FakeUserRepository users) =>
        new(providers, leads, invoices, pdfBuilder, emailSender, users, Config(), NullLoggerFactory.CreateLogger<GenerateAndSendInvoiceHandler>());

    // ---- GenerateAndSendInvoiceHandler ----

    [Fact]
    public async Task Generate_by_non_admin_is_forbidden()
    {
        var invoices = new FakeInvoiceRepository();
        var handler = MakeHandler(new FakeProviderRepository(), new FakeLeadRepository(), invoices, new FakeInvoicePdfBuilder(), new FakeEmailSender(), NonAdminUsers());

        var result = await handler.Handle(new GenerateAndSendInvoiceCommand(NonAdminUserId, Guid.NewGuid(), 2026, 7), default);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
        Assert.Equal(0, invoices.AddCount);
    }

    [Fact]
    public async Task Generate_fails_when_provider_has_no_contact_email()
    {
        var provider = MakeProvider(contactEmail: null);
        var providers = new FakeProviderRepository { ToReturn = provider };
        var invoices = new FakeInvoiceRepository();
        var handler = MakeHandler(providers, new FakeLeadRepository(), invoices, new FakeInvoicePdfBuilder(), new FakeEmailSender(), AdminUsers());

        var result = await handler.Handle(new GenerateAndSendInvoiceCommand(AdminUserId, provider.Id, 2026, 7), default);

        Assert.False(result.Success);
        Assert.Contains("contact email", result.Error);
        Assert.Equal(0, invoices.AddCount);
    }

    [Fact]
    public async Task Generate_is_idempotent_for_an_already_invoiced_period()
    {
        var provider = MakeProvider();
        var providers = new FakeProviderRepository { ToReturn = provider };
        var invoices = new FakeInvoiceRepository
        {
            ExistingForPeriod = new Invoice { ProviderId = provider.Id, InvoiceNumber = "INV-2026-07-existing" }
        };
        var handler = MakeHandler(providers, new FakeLeadRepository(), invoices, new FakeInvoicePdfBuilder(), new FakeEmailSender(), AdminUsers());

        var result = await handler.Handle(new GenerateAndSendInvoiceCommand(AdminUserId, provider.Id, 2026, 7), default);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
        // The core regression this locks in: no second AddAsync call for an already-invoiced period.
        Assert.Equal(0, invoices.AddCount);
    }

    [Fact]
    public async Task Generate_fails_when_no_converted_leads_in_period()
    {
        var provider = MakeProvider();
        var providers = new FakeProviderRepository { ToReturn = provider };
        var leads = new FakeLeadRepository { ConvertedToReturn = Array.Empty<Lead>() };
        var invoices = new FakeInvoiceRepository();
        var handler = MakeHandler(providers, leads, invoices, new FakeInvoicePdfBuilder(), new FakeEmailSender(), AdminUsers());

        var result = await handler.Handle(new GenerateAndSendInvoiceCommand(AdminUserId, provider.Id, 2026, 7), default);

        Assert.False(result.Success);
        Assert.Contains("nothing to invoice", result.Error);
        Assert.Equal(0, invoices.AddCount);
    }

    [Fact]
    public async Task Generate_computes_total_as_lead_count_times_price_per_lead()
    {
        var provider = MakeProvider(pricePerLead: 50m);
        var providers = new FakeProviderRepository { ToReturn = provider };
        var leads = new FakeLeadRepository
        {
            ConvertedToReturn = new[] { MakeConvertedLead(provider.Id), MakeConvertedLead(provider.Id), MakeConvertedLead(provider.Id) }
        };
        var invoices = new FakeInvoiceRepository();
        var emailSender = new FakeEmailSender();
        var handler = MakeHandler(providers, leads, invoices, new FakeInvoicePdfBuilder(), emailSender, AdminUsers());

        var result = await handler.Handle(new GenerateAndSendInvoiceCommand(AdminUserId, provider.Id, 2026, 7), default);

        Assert.True(result.Success);
        Assert.Equal(3, result.Value!.LeadCount);
        Assert.Equal(150m, result.Value.TotalAmount);
        Assert.Equal(1, invoices.AddCount);
        Assert.Single(emailSender.Sent);
        Assert.Equal(provider.ContactEmail, emailSender.Sent[0].ToEmail);
    }

    [Fact]
    public async Task Generate_persists_invoice_as_failed_when_email_send_throws()
    {
        var provider = MakeProvider();
        var providers = new FakeProviderRepository { ToReturn = provider };
        var leads = new FakeLeadRepository { ConvertedToReturn = new[] { MakeConvertedLead(provider.Id) } };
        var invoices = new FakeInvoiceRepository();
        var emailSender = new FakeEmailSender { ThrowOnSend = true };
        var handler = MakeHandler(providers, leads, invoices, new FakeInvoicePdfBuilder(), emailSender, AdminUsers());

        var result = await handler.Handle(new GenerateAndSendInvoiceCommand(AdminUserId, provider.Id, 2026, 7), default);

        // Generation succeeding but delivery failing must NOT surface as a Fail result — the
        // invoice already exists and the admin needs to see its Failed status, not an error page.
        Assert.True(result.Success);
        Assert.Equal((int)InvoiceStatus.Failed, result.Value!.Status);
        Assert.NotNull(result.Value.SendError);
        Assert.Equal(1, invoices.AddCount);
        Assert.NotNull(invoices.MarkedFailed);
        Assert.Null(invoices.MarkedSentId);
    }

    // ---- GetInvoiceStatusHandler ----

    [Fact]
    public async Task GetStatus_by_non_admin_is_forbidden()
    {
        var handler = new GetInvoiceStatusHandler(new FakeInvoiceRepository(), NonAdminUsers(), Config());
        var result = await handler.Handle(new GetInvoiceStatusQuery(NonAdminUserId, Guid.NewGuid(), 2026, 7), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetStatus_returns_null_when_no_invoice_exists_for_period()
    {
        var invoices = new FakeInvoiceRepository { ExistingForPeriod = null };
        var handler = new GetInvoiceStatusHandler(invoices, AdminUsers(), Config());

        var result = await handler.Handle(new GetInvoiceStatusQuery(AdminUserId, Guid.NewGuid(), 2026, 7), default);

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    // ---- GetInvoicePdfHandler ----

    [Fact]
    public async Task GetPdf_by_non_admin_is_forbidden()
    {
        var handler = new GetInvoicePdfHandler(new FakeInvoiceRepository(), new FakeProviderRepository(), new FakeInvoicePdfBuilder(), NonAdminUsers(), Config());
        var result = await handler.Handle(new GetInvoicePdfQuery(NonAdminUserId, Guid.NewGuid()), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetPdf_unknown_invoice_fails()
    {
        var invoices = new FakeInvoiceRepository { ToReturnById = null };
        var handler = new GetInvoicePdfHandler(invoices, new FakeProviderRepository(), new FakeInvoicePdfBuilder(), AdminUsers(), Config());

        var result = await handler.Handle(new GetInvoicePdfQuery(AdminUserId, Guid.NewGuid()), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetPdf_builds_pdf_from_the_matching_invoice_and_provider()
    {
        var provider = MakeProvider();
        var invoice = new Invoice { Id = Guid.NewGuid(), ProviderId = provider.Id };
        var invoices = new FakeInvoiceRepository { ToReturnById = invoice };
        var providers = new FakeProviderRepository { ToReturn = provider };
        var pdfBuilder = new FakeInvoicePdfBuilder();
        var handler = new GetInvoicePdfHandler(invoices, providers, pdfBuilder, AdminUsers(), Config());

        var result = await handler.Handle(new GetInvoicePdfQuery(AdminUserId, invoice.Id), default);

        Assert.True(result.Success);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Value);
        Assert.Same(invoice, pdfBuilder.LastInvoice);
        Assert.Same(provider, pdfBuilder.LastProvider);
    }
}

/// <summary>Minimal no-op ILogger factory — this project has no mocking/logging test library,
/// and GenerateAndSendInvoiceHandler only logs on the email-failure path (already asserted via
/// its return value), so a real no-op logger is enough here.</summary>
internal static class NullLoggerFactory
{
    public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
}
