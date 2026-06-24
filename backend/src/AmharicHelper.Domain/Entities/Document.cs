namespace AmharicHelper.Domain.Entities;

/// <summary>An uploaded official document belonging to a user.</summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Raw text extracted by the OCR provider. Null until OCR runs.</summary>
    public string? OcrText { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
