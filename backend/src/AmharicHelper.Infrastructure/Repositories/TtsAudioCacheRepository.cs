using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

/// <summary>Stores synthesized TTS audio per (document, language, section) so listens are billed once.</summary>
public class TtsAudioCacheRepository(ISqlConnectionFactory factory) : ITtsAudioCacheRepository
{
    public async Task<TtsAudio?> GetAsync(Guid documentId, Language language, SpokenSection section, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<CacheRow>(
            "SELECT ContentType, Audio FROM TtsAudioCache WHERE DocumentId = @documentId AND Language = @language AND Section = @section",
            new { documentId, language = (int)language, section = (int)section });
        return row is null ? null : new TtsAudio(row.Audio, row.ContentType);
    }

    public async Task SetAsync(Guid documentId, Language language, SpokenSection section, TtsAudio audio, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // Upsert: keep a single row per (document, language, section).
        await conn.ExecuteAsync(
            """
            INSERT INTO TtsAudioCache (DocumentId, Language, Section, ContentType, Audio, CreatedAt)
            VALUES (@DocumentId, @Language, @Section, @ContentType, @Audio, now())
            ON CONFLICT (DocumentId, Language, Section) DO UPDATE
                SET ContentType = EXCLUDED.ContentType,
                    Audio = EXCLUDED.Audio,
                    CreatedAt = now();
            """,
            new
            {
                DocumentId = documentId,
                Language = (int)language,
                Section = (int)section,
                audio.ContentType,
                Audio = audio.Content
            });
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM TtsAudioCache WHERE DocumentId = @documentId", new { documentId });
    }

    private class CacheRow
    {
        public string ContentType { get; set; } = "audio/mpeg";
        public byte[] Audio { get; set; } = Array.Empty<byte>();
    }
}
