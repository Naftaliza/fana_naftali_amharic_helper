using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>A required action distilled from a document.</summary>
public record RequiredAction(string Description, bool IsMandatory);

/// <summary>A date/deadline mentioned in a document.</summary>
public record Deadline(DateTime? Date, string Description);

/// <summary>
/// The AI analysis of a single document. JSON-serializable collections are
/// persisted as JSON columns by the Dapper layer.
/// </summary>
public class DocumentAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }

    public string Summary { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public UrgencyLevel UrgencyLevel { get; set; } = UrgencyLevel.Low;

    public IReadOnlyList<string> KeyPoints { get; set; } = new List<string>();
    public IReadOnlyList<RequiredAction> RequiredActions { get; set; } = new List<RequiredAction>();
    public IReadOnlyList<Deadline> Deadlines { get; set; } = new List<Deadline>();

    public string TranslatedAmharic { get; set; } = string.Empty;
    public string TranslatedSimpleHebrew { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
