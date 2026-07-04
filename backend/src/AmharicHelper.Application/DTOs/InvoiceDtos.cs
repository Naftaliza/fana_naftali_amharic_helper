namespace AmharicHelper.Application.DTOs;

/// <summary>A persisted, immutable invoice snapshot for one provider + calendar month.</summary>
public record InvoiceDto(
    Guid Id, Guid ProviderId, string InvoiceNumber, int PeriodYear, int PeriodMonth,
    string Currency, decimal TotalAmount, int LeadCount, int Status, string SentToEmail,
    DateTime GeneratedAt, DateTime? SentAt, string? SendError);

/// <summary>Admin: generate and send an invoice for one provider's given calendar month.</summary>
public record GenerateInvoiceRequest(int Year, int Month);
