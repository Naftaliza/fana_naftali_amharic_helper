using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>Lifecycle of a generated invoice. Draft is never actually reached in this MVP
/// (generation and send happen in one admin action) but is kept so a future "generate first,
/// review, then send" flow doesn't require a schema change.</summary>
public enum InvoiceStatus { Draft = 0, Sent = 1, Failed = 2 }

/// <summary>
/// A snapshot of billable (Converted) leads for one provider over one calendar month, generated
/// on admin demand. Snapshotting here — rather than the live GetSummaryAsync query used by the
/// on-screen Leads tab — means a lead's status changing later (e.g. Converted -> Invalid after a
/// chargeback) never silently changes a historical invoice total.
/// </summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProviderId { get; set; }

    /// <summary>Human-shown identifier, e.g. "INV-2026-07-a1b2c3d4".</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Billing period — the calendar month invoiced.</summary>
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; } // 1-12

    /// <summary>Snapshotted at generation time — even if Provider.PricePerLead changes later.</summary>
    public decimal PricePerLead { get; set; }

    /// <summary>ILS only for this MVP (see plan) — no per-provider currency exists in the data model.</summary>
    public string Currency { get; set; } = "ILS";

    public decimal TotalAmount { get; set; }
    public int LeadCount { get; set; }

    /// <summary>Immutable line-item snapshot (one per billable/Converted lead at generation time).</summary>
    public IReadOnlyList<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Email address the PDF was (or will be) sent to — copied from Provider.ContactEmail
    /// at generation time so a later email change on the provider doesn't rewrite history.</summary>
    public string SentToEmail { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    /// <summary>Set when SMTP send failed; surfaced to the admin so they know delivery didn't happen.</summary>
    public string? SendError { get; set; }
}

/// <summary>One billable lead captured into an invoice at generation time.</summary>
public record InvoiceLineItem(
    Guid LeadId,
    string? Ref,
    DocumentCategory Category,
    DateTime LeadCreatedAt,
    decimal Price);
