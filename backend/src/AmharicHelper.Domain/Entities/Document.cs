using AmharicHelper.Domain.Enums;

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
    /// Every uploaded page is saved here immediately at upload time (even ones OCR later finds
    /// blank/unreadable), because saving now happens before OCR runs — see DocumentProcessor.
    /// </summary>
    public string[] PagePaths { get; set; } = [];

    /// <summary>Content type of each page, same order/length as <see cref="PagePaths"/>. OCR needs
    /// this per page (PDF vs image); it can no longer be inferred from OCR results since OCR runs
    /// later, off the request thread.</summary>
    public string[] PageContentTypes { get; set; } = [];

    /// <summary>Background OCR pipeline state. New uploads start Pending; DocumentProcessor moves
    /// them to Processing, then Ready (or Failed on a config error / zero readable pages).</summary>
    public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending;

    /// <summary>Pages OCR'd so far, for progress polling. Counts both kept and skipped pages.</summary>
    public int ProcessedPages { get; set; }

    /// <summary>Total pages queued for OCR (== PagePaths.Length once the upload completes).</summary>
    public int TotalPages { get; set; }

    /// <summary>Pages that came back blank/unreadable and were excluded from OcrText.</summary>
    public int SkippedPages { get; set; }

    /// <summary>Set when Status is Failed — e.g. a missing Anthropic API key, or every page came
    /// back blank. Null otherwise.</summary>
    public string? ProcessingError { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When RetentionSweepWorker may delete this document (uploaded page files + OCR
    /// text are the sensitive payload — bank, medical, Bituach Leumi letters). Null means "keep
    /// indefinitely" — reserved for a future per-document "pin this one" override; every document
    /// gets a real value today (UploadedAt + 24 months), set at insert time.</summary>
    public DateTime? RetainUntil { get; set; }
}
