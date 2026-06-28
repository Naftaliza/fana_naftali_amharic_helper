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
}

/// <summary>Records each user→provider contact (the billable referral unit).</summary>
public interface ILeadRepository
{
    Task AddAsync(Lead lead, CancellationToken ct = default);
}

public interface IChatMessageRepository
{
    Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task AddAsync(ChatMessage message, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}
