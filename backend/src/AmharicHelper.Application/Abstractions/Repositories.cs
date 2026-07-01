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
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
}

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Document document, CancellationToken ct = default);
    Task UpdateAsync(Document document, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
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
}

public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task AddAsync(ChatMessage message, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}
