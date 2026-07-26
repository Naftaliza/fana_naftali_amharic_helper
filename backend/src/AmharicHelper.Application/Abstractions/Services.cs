using AmharicHelper.Domain.Entities;

namespace AmharicHelper.Application.Abstractions;

/// <summary>Issues and validates JWT access tokens.</summary>
public interface IJwtService
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
}

/// <summary>Hashes and verifies user passwords.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Persists uploaded files to a storage backend (local disk in the MVP).</summary>
public interface IFileStorage
{
    /// <summary>Save the file and return the storage path/key.</summary>
    Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default);
    Task<byte[]> ReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}

/// <summary>
/// Signals the background worker (DocumentProcessingWorker, Infrastructure) that a newly
/// uploaded document is ready for its OCR pipeline (DocumentProcessor) to run. In-memory and
/// single-instance — a crash or redeploy mid-job is recovered by the worker's startup
/// reconciliation pass over documents still Pending/Processing in the database, not by this queue.
/// </summary>
public interface IDocumentProcessingQueue
{
    void Enqueue(Guid documentId);
}

/// <summary>
/// Records a minimal funnel event (see EventNames and AnalyticsEvent). Implementations must never
/// let a tracking failure fail the user-facing operation it's attached to (registration, upload,
/// analysis) — a dropped analytics row is fine; a broken signup because analytics hiccuped is not.
/// </summary>
public interface IEventTracker
{
    Task TrackAsync(string eventName, Guid? userId = null, CancellationToken ct = default);
}
