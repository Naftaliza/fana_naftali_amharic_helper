using AmharicHelper.Application.Common;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;

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

/// <summary>
/// Owns the server-side credit meter (see UsageLedgerEntry). This is the only interface allowed
/// to write UsageLedger rows — every write is transactional and serialized per subject (a
/// Postgres advisory lock keyed on the subject), so two concurrent requests from the same
/// low-balance subject can never both succeed against the same last credit. A subject with no
/// grants yet is lazily given a monthly free-tier allowance the first time they try to spend,
/// rather than requiring a signup-time grant or a cron job.
/// </summary>
public interface IWalletService
{
    Task<int> GetBalanceAsync(UsageSubject subject, CancellationToken ct = default);

    /// <summary>Attempts to spend exactly 1 credit for <paramref name="operation"/>. Lazily grants
    /// the monthly free tier first if the subject hasn't received one yet this calendar month and
    /// has no other balance. Returns false (and consumes nothing) if the subject is out of
    /// credits — callers must not proceed with the paid operation in that case.</summary>
    Task<bool> TryConsumeAsync(
        UsageSubject subject, string operation, Guid? documentId = null, CancellationToken ct = default);

    Task GrantAsync(
        UsageSubject subject, int credits, UsageLedgerKind kind, string note, CancellationToken ct = default);

    /// <summary>Deducts <paramref name="credits"/> from the subject's balance for an outbound
    /// sponsorship gift — atomic and serialized per subject like TryConsumeAsync, but unlike it,
    /// this never triggers the lazy monthly free-tier grant. Gifting isn't "trying the product",
    /// so it shouldn't manufacture credits that didn't already exist; a sponsor can only give away
    /// credits they already have. Returns false (no write) if the current balance can't cover it.</summary>
    Task<bool> TryDebitForSponsorshipAsync(
        UsageSubject subject, int credits, string note, CancellationToken ct = default);

    /// <summary>Credits a beneficiary on successful Sponsorship redemption. A thin wrapper over
    /// GrantAsync with Operation recorded as "sponsorship_received" instead of "grant", so wallet
    /// history can tell a received gift apart from an admin top-up.</summary>
    Task GrantSponsorshipAsync(UsageSubject subject, int credits, string note, CancellationToken ct = default);

    Task<IReadOnlyList<UsageLedgerEntry>> GetHistoryAsync(
        UsageSubject subject, int limit, CancellationToken ct = default);
}
