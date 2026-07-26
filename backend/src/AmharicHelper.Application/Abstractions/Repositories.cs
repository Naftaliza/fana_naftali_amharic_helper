using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);

    /// <summary>Permanently deletes the account row (GDPR Art. 17 "right to erasure"). Callers
    /// must delete the user's documents and refresh tokens first — Users has no cascading FKs.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);

    /// <summary>Deletes every refresh token belonging to a user — required before the user row
    /// itself can be deleted (RefreshTokens.UserId has no ON DELETE CASCADE).</summary>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Document document, CancellationToken ct = default);

    /// <summary>Persists OcrText and the processing-state fields (Status, ProcessedPages,
    /// TotalPages, SkippedPages, ProcessingError) — called repeatedly by DocumentProcessor as it
    /// works through a document's pages. Immutable fields (FileName, PagePaths, ...) are not
    /// re-written here.</summary>
    Task UpdateAsync(Document document, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Documents left Pending/Processing — either freshly queued, or orphaned by a crash
    /// or redeploy mid-job. Used by DocumentProcessingWorker on startup to re-queue unfinished work,
    /// since the in-memory queue itself doesn't survive a restart.</summary>
    Task<IReadOnlyList<Document>> ListUnfinishedAsync(CancellationToken ct = default);
}

public interface IDocumentAnalysisRepository
{
    Task<DocumentAnalysis?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
    Task AddAsync(DocumentAnalysis analysis, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>
/// Caches synthesized TTS audio per (document, language) so repeated listens don't
/// re-bill the speech provider. Invalidated when the document is re-analyzed or deleted.
/// </summary>
public interface ITtsAudioCacheRepository
{
    Task<TtsAudio?> GetAsync(Guid documentId, Language language, CancellationToken ct = default);
    Task SetAsync(Guid documentId, Language language, TtsAudio audio, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>Vetted professionals shown as sponsored referrals, matched by document category.</summary>
public interface IProviderRepository
{
    Task<IReadOnlyList<Provider>> GetActiveByCategoryAsync(DocumentCategory category, int limit, CancellationToken ct = default);
    Task<Provider?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Stores a new (pending, inactive) provider application.</summary>
    Task AddAsync(Provider provider, CancellationToken ct = default);

    /// <summary>Never-reviewed applications awaiting a first decision, newest first.</summary>
    Task<IReadOnlyList<Provider>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>Providers an admin has acted on (live + deactivated), live first.</summary>
    Task<IReadOnlyList<Provider>> GetManagedAsync(CancellationToken ct = default);

    /// <summary>Approve/activate (true) or deactivate (false) a provider; stamps ReviewedAt.</summary>
    Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default);

    /// <summary>Update a provider's editable fields (not its active/review state).</summary>
    Task UpdateAsync(Provider provider, CancellationToken ct = default);

    /// <summary>Remove a provider (e.g. rejecting an application).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Records each user→provider contact (the billable referral unit).</summary>
public interface ILeadRepository
{
    Task AddAsync(Lead lead, CancellationToken ct = default);

    /// <summary>Per-provider lead counts (this month + all time), busiest first.</summary>
    Task<IReadOnlyList<LeadSummaryDto>> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>The most recent leads, newest first.</summary>
    Task<IReadOnlyList<RecentLeadDto>> GetRecentAsync(int limit, CancellationToken ct = default);

    /// <summary>Update a lead's lifecycle status (admin action).</summary>
    Task UpdateStatusAsync(Guid leadId, LeadStatus status, CancellationToken ct = default);

    /// <summary>Record the anonymous post-contact "did this help?" signal for the lead matching
    /// this ref code. Returns false if no lead has that ref.</summary>
    Task<bool> SetFeedbackAsync(string refCode, bool helpful, CancellationToken ct = default);

    /// <summary>Converted leads for one provider within a calendar month — the exact rows an
    /// invoice snapshots as line items. Ordered by CreatedAt.</summary>
    Task<IReadOnlyList<Lead>> GetConvertedForPeriodAsync(Guid providerId, int year, int month, CancellationToken ct = default);
}

public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task AddAsync(ChatMessage message, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>B2B/B2G tenants (see <see cref="Organization"/>) and their aggregate usage.</summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>All tenants, newest first.</summary>
    Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default);

    Task AddAsync(Organization organization, CancellationToken ct = default);

    /// <summary>Update a tenant's editable branding fields (not Slug, not IsActive).</summary>
    Task UpdateAsync(Organization organization, CancellationToken ct = default);

    /// <summary>Activate or deactivate a tenant — gates the public branding lookup.</summary>
    Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default);

    /// <summary>Aggregate, anonymized usage for one tenant — documents processed, unique
    /// members, and the category/urgency/weekly breakdowns behind the admin dashboard.</summary>
    Task<OrganizationStatsDto> GetStatsAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>Minimal funnel-event storage (see <see cref="AnalyticsEvent"/> and IEventTracker).</summary>
public interface IAnalyticsEventRepository
{
    Task AddAsync(AnalyticsEvent evt, CancellationToken ct = default);

    /// <summary>Event counts since a cutoff, one row per distinct event name — the funnel view
    /// behind the admin analytics endpoint.</summary>
    Task<IReadOnlyList<EventCountDto>> GetFunnelCountsAsync(DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Individual occurrences of one event since a cutoff, newest first — the click-through
    /// list behind a funnel bar. Email is null when the account has since been deleted.</summary>
    Task<IReadOnlyList<EventDetailDto>> GetEventDetailsAsync(string name, DateTime sinceUtc, CancellationToken ct = default);
}

/// <summary>Persisted, immutable invoice snapshots for provider billing (see <see cref="Invoice"/>).</summary>
public interface IInvoiceRepository
{
    /// <summary>Null if no invoice exists yet for this provider+month (the idempotency check).</summary>
    Task<Invoice?> GetByProviderAndPeriodAsync(Guid providerId, int year, int month, CancellationToken ct = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>All invoices for one provider, newest period first.</summary>
    Task<IReadOnlyList<Invoice>> ListByProviderAsync(Guid providerId, CancellationToken ct = default);

    Task AddAsync(Invoice invoice, CancellationToken ct = default);

    Task MarkSentAsync(Guid id, DateTime sentAt, CancellationToken ct = default);

    Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default);
}
