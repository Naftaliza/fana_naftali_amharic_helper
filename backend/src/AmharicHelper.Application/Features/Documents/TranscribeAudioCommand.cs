using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Documents;

/// <summary>
/// Speech-to-text for the chat "ask out loud" mic button. Deliberately unmetered — unlike
/// analyze/speech/chat, a single question's transcription costs a fraction of a cent (Fast
/// Transcription bills per audio-hour), and charging a credit just to ask a question would
/// punish exactly the user this feature exists for: someone who can't type Amharic. Abuse is
/// bounded by the existing documents/trial rate-limit policies and the request-size cap already
/// enforced by the controller, not by the wallet — voice can't be used to obtain free AI, since
/// the chat/analyze call that follows a transcript is already metered on its own.
///
/// Shared verbatim by both the authenticated (DocumentsController) and anonymous
/// (TrialController) callers — there's no per-subject usage state to track since nothing here
/// is charged.
/// </summary>
public record TranscribeAudioCommand(Stream Audio, string FileName, string ContentType, Language Language)
    : IRequest<Result<TranscriptDto>>;

public class TranscribeAudioHandler(ISttProvider stt) : IRequestHandler<TranscribeAudioCommand, Result<TranscriptDto>>
{
    public async Task<Result<TranscriptDto>> Handle(TranscribeAudioCommand cmd, CancellationToken ct)
    {
        try
        {
            var result = await stt.TranscribeAsync(cmd.Audio, cmd.FileName, cmd.ContentType, cmd.Language, ct);
            if (string.IsNullOrWhiteSpace(result.Text))
                return Result<TranscriptDto>.Fail("EMPTY_TRANSCRIPT");
            return Result<TranscriptDto>.Ok(new TranscriptDto(result.Text, result.Locale));
        }
        catch (InvalidOperationException ex)
        {
            // Mirrors the ITtsProvider convention: a missing key or a non-2xx Azure response
            // surfaces as a plain error string the controller turns into a 400.
            return Result<TranscriptDto>.Fail(ex.Message);
        }
    }
}
