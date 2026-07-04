using System.Text.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class InvoiceRepository(ISqlConnectionFactory factory) : IInvoiceRepository
{
    public async Task<Invoice?> GetByProviderAndPeriodAsync(Guid providerId, int year, int month, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<InvoiceRow>(
            "SELECT * FROM Invoices WHERE ProviderId = @providerId AND PeriodYear = @year AND PeriodMonth = @month",
            new { providerId, year, month });
        return row?.ToEntity();
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<InvoiceRow>("SELECT * FROM Invoices WHERE Id = @id", new { id });
        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<Invoice>> ListByProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<InvoiceRow>(
            "SELECT * FROM Invoices WHERE ProviderId = @providerId ORDER BY PeriodYear DESC, PeriodMonth DESC",
            new { providerId });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // The unique index (ProviderId, PeriodYear, PeriodMonth) is the DB-level backstop for a
        // race (two admins generating the same provider+month simultaneously); the handler's
        // pre-check is the primary guard. A genuine race surfaces as an unhandled 500 via
        // ExceptionHandlingMiddleware — same convention as CreateOrganizationHandler's slug check.
        await conn.ExecuteAsync(
            """
            INSERT INTO Invoices
                (Id, ProviderId, InvoiceNumber, PeriodYear, PeriodMonth, PricePerLead, Currency,
                 TotalAmount, LeadCount, LineItemsJson, Status, SentToEmail, GeneratedAt, SentAt, SendError)
            VALUES
                (@Id, @ProviderId, @InvoiceNumber, @PeriodYear, @PeriodMonth, @PricePerLead, @Currency,
                 @TotalAmount, @LeadCount, @LineItemsJson, @Status, @SentToEmail, @GeneratedAt, @SentAt, @SendError)
            """,
            new
            {
                invoice.Id,
                invoice.ProviderId,
                invoice.InvoiceNumber,
                invoice.PeriodYear,
                invoice.PeriodMonth,
                invoice.PricePerLead,
                invoice.Currency,
                invoice.TotalAmount,
                invoice.LeadCount,
                LineItemsJson = JsonSerializer.Serialize(invoice.LineItems),
                Status = (int)invoice.Status,
                invoice.SentToEmail,
                invoice.GeneratedAt,
                invoice.SentAt,
                invoice.SendError
            });
    }

    public async Task MarkSentAsync(Guid id, DateTime sentAt, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            "UPDATE Invoices SET Status = 1, SentAt = @sentAt, SendError = NULL WHERE Id = @id",
            new { id, sentAt });
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            "UPDATE Invoices SET Status = 2, SendError = @error WHERE Id = @id",
            new { id, error });
    }

    /// <summary>Raw row matching the SQL columns; LineItemsJson is deserialized in <see cref="ToEntity"/>.</summary>
    private class InvoiceRow
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal PricePerLead { get; set; }
        public string Currency { get; set; } = "ILS";
        public decimal TotalAmount { get; set; }
        public int LeadCount { get; set; }
        public string LineItemsJson { get; set; } = "[]";
        public int Status { get; set; }
        public string SentToEmail { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public string? SendError { get; set; }

        public Invoice ToEntity() => new()
        {
            Id = Id,
            ProviderId = ProviderId,
            InvoiceNumber = InvoiceNumber,
            PeriodYear = PeriodYear,
            PeriodMonth = PeriodMonth,
            PricePerLead = PricePerLead,
            Currency = Currency,
            TotalAmount = TotalAmount,
            LeadCount = LeadCount,
            LineItems = JsonSerializer.Deserialize<List<InvoiceLineItem>>(LineItemsJson) ?? new(),
            Status = (InvoiceStatus)Status,
            SentToEmail = SentToEmail,
            GeneratedAt = GeneratedAt,
            SentAt = SentAt,
            SendError = SendError
        };
    }
}
