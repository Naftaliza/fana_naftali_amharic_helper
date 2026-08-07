namespace AmharicHelper.Domain.Entities;

/// <summary>
/// A versioned Terms-of-service or Privacy-policy document, trilingual. Versioned rather than
/// edited in place so a historical ConsentRecord always points at the exact text a user agreed
/// to, even after the current version changes.
/// </summary>
public class LegalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>"terms" or "privacy".</summary>
    public string Kind { get; set; } = string.Empty;

    public int Version { get; set; } = 1;
    public string BodyHe { get; set; } = string.Empty;
    public string BodyAm { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Records that a subject (an account, or a not-yet-registered contact such as a WhatsApp
/// number) accepted a specific version of a LegalDocument. Evidence for GDPR Art. 7(1) /
/// Israeli Privacy Protection Law amendment 13. Never updated after insertion.
/// </summary>
public class ConsentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }

    /// <summary>SHA-256 hex of a not-yet-registered contact's identifier (e.g. an E.164 phone
    /// number). Never the raw identifier — see UsageSubject.</summary>
    public string? ContactHash { get; set; }

    public string Kind { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public string? SourceIp { get; set; }
}
