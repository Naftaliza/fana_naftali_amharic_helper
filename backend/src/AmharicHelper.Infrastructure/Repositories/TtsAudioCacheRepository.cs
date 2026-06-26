using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

/// <summary>Stores synthesized TTS audio per (document, language) so listens are billed once.</summary>
public class TtsAudioCacheRepository(ISqlConnectionFactory factory) : ITtsAudioCacheRepository
{
    public async Task<TtsAudio?> GetAsync(Guid documentId, Language language, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<CacheRow>(
            "SELECT ContentType, Audio FROM dbo.TtsAudioCache WHERE DocumentId = @documentId AND Language = @language",
            new { documentId, language = (int)language });
        return row is null ? null : new TtsAudio(row.Audio, row.ContentType);
    }

    public async Task SetAsync(Guid documentId, Language language, TtsAudio audio, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // Upsert: keep a single row per (document, language).
        await conn.ExecuteAsync(
            """
            MERGE dbo.TtsAudioCache AS target
            USING (SELECT @DocumentId AS DocumentId, @Language AS Language) AS src
                ON target.DocumentId = src.DocumentId AND target.Language = src.Language
            WHEN MATCHED THEN
                UPDATE SET ContentType = @ContentType, Audio = @Audio, CreatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (DocumentId, Language, ContentType, Audio)
                VALUES (@DocumentId, @Language, @ContentType, @Audio);
            """,
            new
            {
                DocumentId = documentId,
                Language = (int)language,
                audio.ContentType,
                Audio = audio.Content
            });
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM dbo.TtsAudioCache WHERE DocumentId = @documentId", new { documentId });
    }

    private class CacheRow
    {
        public string ContentType { get; set; } = "audio/mpeg";
        public byte[] Audio { get; set; } = Array.Empty<byte>();
    }
}
