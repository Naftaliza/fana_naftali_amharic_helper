using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>
/// A piece of text available in all three supported languages. The analysis is
/// generated once with every textual field translated, so the UI and the
/// text-to-speech can render a single, consistent language without mixing.
/// </summary>
public sealed class LocalizedText
{
    public string He { get; set; } = string.Empty;
    public string Am { get; set; } = string.Empty;
    public string En { get; set; } = string.Empty;

    public LocalizedText() { }

    public LocalizedText(string he, string am, string en)
    {
        He = he;
        Am = am;
        En = en;
    }

    /// <summary>The text in the requested language, falling back to another language if blank.</summary>
    public string For(Language language) => language switch
    {
        Language.Amharic => Pick(Am, En, He),
        Language.English => Pick(En, He, Am),
        _ => Pick(He, En, Am),
    };

    private static string Pick(params string[] options)
    {
        foreach (var s in options)
            if (!string.IsNullOrWhiteSpace(s)) return s;
        return string.Empty;
    }
}

/// <summary>A required action distilled from a document.</summary>
public record RequiredAction(LocalizedText Description, bool IsMandatory);

/// <summary>A date/deadline mentioned in a document.</summary>
public record Deadline(DateTime? Date, LocalizedText Description);

/// <summary>
/// The AI analysis of a single document. Every textual field is localized to all three
/// languages. JSON-serializable collections are persisted as JSON columns by the Dapper layer.
/// </summary>
public class DocumentAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }

    public LocalizedText Summary { get; set; } = new();
    public LocalizedText DocumentType { get; set; } = new();

    /// <summary>AI-classified institution category, used to match sponsored referrals.</summary>
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;

    public UrgencyLevel UrgencyLevel { get; set; } = UrgencyLevel.Low;

    public IReadOnlyList<LocalizedText> KeyPoints { get; set; } = new List<LocalizedText>();
    public IReadOnlyList<RequiredAction> RequiredActions { get; set; } = new List<RequiredAction>();
    public IReadOnlyList<Deadline> Deadlines { get; set; } = new List<Deadline>();

    /// <summary>A longer plain-language walkthrough of the document, in each language.</summary>
    public LocalizedText Explanation { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
