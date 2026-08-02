using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.Tts;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

/// <summary>Produce spoken audio of a document's analysis in the requested language. Section
/// defaults to the whole walkthrough; a specific section serves per-card "read just the
/// actions" playback instead.</summary>
public record SpeakDocumentQuery(Guid UserId, Guid DocumentId, Language Language, SpokenSection Section = SpokenSection.Full)
    : IRequest<Result<TtsAudio>>;

public class SpeakDocumentHandler(
    IDocumentRepository documents,
    IDocumentAnalysisRepository analyses,
    ITtsAudioCacheRepository cache,
    ITtsProvider tts,
    IWalletService wallet) : IRequestHandler<SpeakDocumentQuery, Result<TtsAudio>>
{
    public async Task<Result<TtsAudio>> Handle(SpeakDocumentQuery q, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(q.DocumentId, ct);
        if (doc is null || doc.UserId != q.UserId)
            return Result<TtsAudio>.Fail("Document not found.");

        // Serve previously synthesized audio for this document+language+section for free — no
        // credit is charged, since replaying a cached clip costs the app nothing.
        var cached = await cache.GetAsync(q.DocumentId, q.Language, q.Section, ct);
        if (cached is not null)
            return Result<TtsAudio>.Ok(cached);

        var analysis = await analyses.GetByDocumentIdAsync(q.DocumentId, ct);
        if (analysis is null)
            return Result<TtsAudio>.Fail("Document has not been analyzed yet.");

        var text = SpokenTextBuilder.Build(analysis, q.Language, q.Section);
        if (string.IsNullOrWhiteSpace(text))
            return Result<TtsAudio>.Fail("Nothing to read for this document.");

        // Only reachable on a cache miss, i.e. only when a paid Azure/ElevenLabs call is about
        // to actually happen.
        if (!await wallet.TryConsumeAsync(UsageSubject.ForUser(q.UserId), "tts", q.DocumentId, ct))
            return Result<TtsAudio>.Fail(WalletErrors.OutOfCredits);

        try
        {
            var audio = await tts.SynthesizeAsync(text, q.Language, ct);
            // Cache so this document+language+section is never billed again until re-analyzed.
            await cache.SetAsync(q.DocumentId, q.Language, q.Section, audio, ct);
            return Result<TtsAudio>.Ok(audio);
        }
        catch (InvalidOperationException ex)
        {
            // Surface configuration / provider errors (missing or invalid key, etc.) clearly.
            return Result<TtsAudio>.Fail(ex.Message);
        }
    }
}
