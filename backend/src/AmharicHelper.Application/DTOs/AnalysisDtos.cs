using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

/// <summary>
/// The exact analysis shape the AI returns, matching the product spec:
/// summary, documentType, urgencyLevel, keyPoints, requiredActions, deadlines,
/// translatedAmharic, translatedSimpleHebrew.
/// </summary>
public class DocumentAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public UrgencyLevel UrgencyLevel { get; set; } = UrgencyLevel.Low;
    public List<string> KeyPoints { get; set; } = new();
    public List<RequiredActionDto> RequiredActions { get; set; } = new();
    public List<DeadlineDto> Deadlines { get; set; } = new();
    public string TranslatedAmharic { get; set; } = string.Empty;
    public string TranslatedSimpleHebrew { get; set; } = string.Empty;
}

public class RequiredActionDto
{
    public string Description { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}

public class DeadlineDto
{
    public DateTime? Date { get; set; }
    public string Description { get; set; } = string.Empty;
}
