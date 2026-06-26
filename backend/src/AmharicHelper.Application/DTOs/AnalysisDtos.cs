using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

/// <summary>
/// The analysis shape the AI returns. Every textual field is localized to all three
/// languages (he/am/en) so the UI and speech can stay in a single, consistent language.
/// </summary>
public class DocumentAnalysisResult
{
    public LocalizedText Summary { get; set; } = new();
    public LocalizedText DocumentType { get; set; } = new();
    public UrgencyLevel UrgencyLevel { get; set; } = UrgencyLevel.Low;
    public List<LocalizedText> KeyPoints { get; set; } = new();
    public List<RequiredActionDto> RequiredActions { get; set; } = new();
    public List<DeadlineDto> Deadlines { get; set; } = new();
    public LocalizedText Explanation { get; set; } = new();
}

public class RequiredActionDto
{
    public LocalizedText Description { get; set; } = new();
    public bool IsMandatory { get; set; }
}

public class DeadlineDto
{
    public DateTime? Date { get; set; }
    public LocalizedText Description { get; set; } = new();
}
