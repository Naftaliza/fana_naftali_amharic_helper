using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.Features.Wallet;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace AmharicHelper.Infrastructure.Wallet;

/// <summary>
/// Real implementation of the server-side credit meter. TryConsumeAsync is the one method that
/// must be race-safe: two simultaneous requests from a subject sitting on exactly 1 credit must
/// not both succeed. Rather than a SELECT-then-UPDATE race on a mutable balance column (there
/// isn't one — see UsageLedgerEntry), every write happens inside a transaction that first takes a
/// Postgres session-level advisory lock scoped to the subject (pg_advisory_xact_lock), so a
/// second concurrent caller for the same subject simply blocks until the first transaction
/// commits or rolls back, then sees the up-to-date balance. The lock is released automatically
/// at transaction end — no separate unlock call, so it can't leak on an exception.
/// </summary>
public class WalletService(ISqlConnectionFactory factory) : IWalletService
{
    private const string BalanceSql =
        "SELECT COALESCE(SUM(Delta), 0) FROM UsageLedger WHERE (@UserId::uuid IS NOT NULL AND UserId = @UserId) OR (@ContactHash IS NOT NULL AND ContactHash = @ContactHash)";

    private const string InsertSql =
        """
        INSERT INTO UsageLedger (Id, UserId, ContactHash, Kind, Delta, Operation, CostUsd, DocumentId, Note, CreatedAt)
        VALUES (@Id, @UserId, @ContactHash, @Kind, @Delta, @Operation, @CostUsd, @DocumentId, @Note, @CreatedAt)
        """;

    public async Task<int> GetBalanceAsync(UsageSubject subject, CancellationToken ct = default)
    {
        ValidateSubject(subject);
        using var conn = factory.Create();
        return await conn.ExecuteScalarAsync<int>(BalanceSql, new { subject.UserId, subject.ContactHash });
    }

    public async Task<bool> TryConsumeAsync(
        UsageSubject subject, string operation, Guid? documentId = null, CancellationToken ct = default)
    {
        ValidateSubject(subject);

        using var conn = new NpgsqlConnection(factory.ConnectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Serializes every consume/grant for this exact subject. hashtext() only has 32 bits
            // of entropy, which is fine here — the goal is to scope contention, not to guarantee
            // global uniqueness of the lock key.
            var lockKey = SubjectKey(subject);
            await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey)::bigint)", new { lockKey }, tx);

            var balance = await conn.ExecuteScalarAsync<int>(BalanceSql, new { subject.UserId, subject.ContactHash }, tx);

            if (balance <= 0)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var alreadyGrantedThisMonth = await conn.ExecuteScalarAsync<bool>(
                    """
                    SELECT EXISTS(
                        SELECT 1 FROM UsageLedger
                        WHERE ((@UserId::uuid IS NOT NULL AND UserId = @UserId) OR (@ContactHash IS NOT NULL AND ContactHash = @ContactHash))
                          AND Operation = 'free_tier_monthly' AND CreatedAt >= @monthStart
                    )
                    """,
                    new { subject.UserId, subject.ContactHash, monthStart }, tx);

                if (!alreadyGrantedThisMonth)
                {
                    await conn.ExecuteAsync(InsertSql, NewEntry(
                        subject, UsageLedgerKind.Grant, WalletDefaults.FreeTierMonthlyCredits,
                        "free_tier_monthly", null, "Monthly free tier"), tx);
                    balance += WalletDefaults.FreeTierMonthlyCredits;
                }
            }

            if (balance <= 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            await conn.ExecuteAsync(InsertSql, NewEntry(subject, UsageLedgerKind.Consume, -1, operation, documentId, ""), tx);
            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task GrantAsync(
        UsageSubject subject, int credits, UsageLedgerKind kind, string note, CancellationToken ct = default)
    {
        ValidateSubject(subject);
        using var conn = factory.Create();
        await conn.ExecuteAsync(InsertSql, NewEntry(subject, kind, credits, "grant", null, note));
    }

    public async Task<bool> TryDebitForSponsorshipAsync(
        UsageSubject subject, int credits, string note, CancellationToken ct = default)
    {
        ValidateSubject(subject);
        if (credits <= 0)
            throw new ArgumentException("Credits must be positive.", nameof(credits));

        using var conn = new NpgsqlConnection(factory.ConnectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Same per-subject advisory lock as TryConsumeAsync, but no free-tier grant branch —
            // gifting isn't "trying the product", so a sponsor can only ever give away credits
            // they already have.
            var lockKey = SubjectKey(subject);
            await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey)::bigint)", new { lockKey }, tx);

            var balance = await conn.ExecuteScalarAsync<int>(BalanceSql, new { subject.UserId, subject.ContactHash }, tx);
            if (balance < credits)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            await conn.ExecuteAsync(InsertSql,
                NewEntry(subject, UsageLedgerKind.Sponsorship, -credits, "sponsorship_sent", null, note), tx);
            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task GrantSponsorshipAsync(UsageSubject subject, int credits, string note, CancellationToken ct = default)
    {
        ValidateSubject(subject);
        using var conn = factory.Create();
        await conn.ExecuteAsync(InsertSql,
            NewEntry(subject, UsageLedgerKind.Sponsorship, credits, "sponsorship_received", null, note));
    }

    public async Task<IReadOnlyList<UsageLedgerEntry>> GetHistoryAsync(
        UsageSubject subject, int limit, CancellationToken ct = default)
    {
        ValidateSubject(subject);
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<UsageLedgerEntry>(
            """
            SELECT * FROM UsageLedger
            WHERE (@UserId::uuid IS NOT NULL AND UserId = @UserId) OR (@ContactHash IS NOT NULL AND ContactHash = @ContactHash)
            ORDER BY CreatedAt DESC LIMIT @limit
            """,
            new { subject.UserId, subject.ContactHash, limit });
        return rows.ToList();
    }

    private static void ValidateSubject(UsageSubject subject)
    {
        if (subject.UserId is null && string.IsNullOrWhiteSpace(subject.ContactHash))
            throw new ArgumentException("A usage subject must have either a UserId or a ContactHash.");
    }

    private static string SubjectKey(UsageSubject subject) => $"{subject.UserId}|{subject.ContactHash}";

    private static UsageLedgerEntry NewEntry(
        UsageSubject subject, UsageLedgerKind kind, int delta, string operation, Guid? documentId, string note) => new()
    {
        Id = Guid.NewGuid(),
        UserId = subject.UserId,
        ContactHash = subject.ContactHash,
        Kind = kind,
        Delta = delta,
        Operation = operation,
        DocumentId = documentId,
        Note = note,
        CreatedAt = DateTime.UtcNow
    };
}
