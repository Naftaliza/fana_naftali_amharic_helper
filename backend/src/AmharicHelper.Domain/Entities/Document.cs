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

    /// <summary>
    /// Ordered list of stored file paths, one per page (page 0 also lives in <see cref="FilePath"/>
    /// for back-compat). Empty for legacy single-file documents. Authoritative for file cleanup.
    /// </summary>
    public string[] PagePaths { get; set; } = [];

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
