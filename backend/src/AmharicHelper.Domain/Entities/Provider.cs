using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>
/// A vetted professional (accountant, lawyer, insurance agent, ...) shown to users as a
/// paid referral, matched to the <see cref="DocumentCategory"/> of the document they just
/// had analyzed. The business pays per delivered <see cref="Lead"/>, or a flat listing fee.
/// </summary>
public class Provider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DocumentCategory Category { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? City { get; set; }

    /// <summary>How to reach the applicant business. Set when they self-register.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Short "what I help with" blurb, localized to all three languages.</summary>
    public LocalizedText Blurb { get; set; } = new();

    public bool IsActive { get; set; } = true;

    /// <summary>Higher shows first (ties broken by name) — lets you promote paying providers.</summary>
    public int Priority { get; set; }

    /// <summary>What this provider pays per delivered lead, used for monthly invoicing.</summary>
    public decimal PricePerLead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One user→provider contact event (a tap on "Contact"/"WhatsApp"/"Call"). This is the
/// billable unit: count leads per provider per month to invoice. No personal user data is
/// stored — only which provider was contacted, from what document category and urgency.
/// </summary>
public class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProviderId { get; set; }
    public DocumentCategory Category { get; set; }
    public UrgencyLevel Urgency { get; set; }

    /// <summary>The source document, when known. Null for anonymous trial analyses.</summary>
    public Guid? DocumentId { get; set; }

    /// <summary>Short human-readable code shown to the user, in the WhatsApp message, and on the
    /// admin Leads tab — lets the provider quote it so you can match/confirm the lead.</summary>
    public string? Ref { get; set; }

    /// <summary>Lifecycle status, editable by admin — billing can key off Converted instead of
    /// the raw contact tap.</summary>
    public LeadStatus Status { get; set; } = LeadStatus.New;

    /// <summary>Whether the user reported the provider actually helped, set via the anonymous
    /// post-contact feedback prompt (keyed by <see cref="Ref"/>). Null until rated.</summary>
    public bool? Helpful { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
