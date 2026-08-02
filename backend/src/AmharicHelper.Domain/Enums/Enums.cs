namespace AmharicHelper.Domain.Enums;

/// <summary>How urgent acting on a document is.</summary>
public enum UrgencyLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>The kind of institution / document. Drives which prompt template is used.</summary>
public enum DocumentCategory
{
    Government = 0,
    Bank = 1,
    Insurance = 2,
    Employment = 3,
    Healthcare = 4,
    Municipality = 5,
    Other = 6
}

/// <summary>Supported UI / output languages. Hebrew is the default; only Hebrew is RTL.</summary>
public enum Language
{
    Hebrew = 0,
    Amharic = 1,
    English = 2
}

/// <summary>Which passage of a spoken analysis to synthesize. Full is the whole walkthrough
/// (the historical, only behavior) — a specific section lets the frontend offer per-card
/// "read just the actions" playback instead of all-or-nothing audio. Full = 0 keeps every
/// TtsAudioCache row written before per-section audio existed valid without a backfill.</summary>
public enum SpokenSection
{
    Full = 0,
    Summary = 1,
    Explanation = 2,
    KeyPoints = 3,
    Actions = 4,
    Deadlines = 5
}

/// <summary>Author of a chat message.</summary>
public enum ChatRole
{
    User = 0,
    Assistant = 1
}

/// <summary>Where a lead stands in its lifecycle. Lets billing be based on real conversions
/// rather than a raw contact tap.</summary>
public enum LeadStatus
{
    New = 0,
    Contacted = 1,
    Responded = 2,
    Converted = 3,
    Invalid = 4
}

/// <summary>
/// Background OCR pipeline state for an uploaded document (see DocumentProcessor). Existing rows
/// predating this column default to Ready, since their OCR already ran synchronously at upload.
/// </summary>
public enum DocumentProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3
}

/// <summary>The kind of movement recorded in <see cref="Entities.UsageLedgerEntry"/>. Balance for
/// a subject is always SUM(Delta) over their rows — never a mutable counter — so every charge is
/// individually auditable and a bug can't silently corrupt a running total.</summary>
public enum UsageLedgerKind
{
    Grant = 0,
    Consume = 1,
    Expire = 2,
    Refund = 3,
    Sponsorship = 4
}
