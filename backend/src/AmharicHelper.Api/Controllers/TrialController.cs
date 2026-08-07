using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Application.Features.Trial;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Anonymous "try it" endpoints. No authentication, no persistence — results are returned
/// in-memory only. Server-side metered via UsageLedger (see IWalletService) against a subject
/// built from the client's device id (frontend/lib/deviceId.ts, sent as X-Device-Id) — this
/// replaced the old client-only lib/trial.ts localStorage counter, which any user could reset by
/// clearing browser storage. A caller with no device id header falls back to a per-IP subject:
/// coarser (shared by every request from the same address) but still a real server-side ceiling.
/// </summary>
[ApiController]
[Route("api/trial")]
[AllowAnonymous]
[EnableRateLimiting("trial")]
public class TrialController(IMediator mediator) : ControllerBase
{
    // Trial batches are capped tighter than logged-in uploads: the trial rate limit is per-request,
    // so a single huge batch would otherwise evade the cost control on anonymous Claude usage.
    private const int MaxTrialPages = 5;

    /// <summary>Upload one or more pages and get a generic analysis without signing in or saving anything.</summary>
    [HttpPost("analyze")]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> Analyze(List<IFormFile> files)
    {
        var (pages, error) = await UploadValidation.BuildPagesAsync(files, MaxTrialPages, HttpContext.RequestAborted);
        if (error is not null) return BadRequest(new { error });

        var result = await mediator.Send(new AnalyzeTrialCommand(pages!, Subject()));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Read a trial analysis aloud (MP3) in the requested language.</summary>
    [HttpPost("speech")]
    public async Task<IActionResult> Speech(TrialSpeechRequest request)
    {
        var result = await mediator.Send(new SpeakTrialQuery(request.Analysis, request.Language, Subject(), request.Section));
        if (!result.Success || result.Value is null)
            return BadRequest(new { error = result.Error });
        return File(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>Transcribe a short voice recording without signing in — anonymous twin of
    /// DocumentsController's endpoint, under the tighter per-IP `trial` rate limit. Unmetered,
    /// same reasoning as the authenticated endpoint.</summary>
    [HttpPost("transcribe")]
    [RequestSizeLimit(8_000_000)]
    public async Task<IActionResult> Transcribe(IFormFile audio, [FromQuery] Language language = Language.Hebrew)
    {
        if (audio is null || audio.Length == 0) return BadRequest(new { error = "No audio provided." });
        await using var stream = audio.OpenReadStream();
        var result = await mediator.Send(new TranscribeAudioCommand(stream, audio.FileName, audio.ContentType, language));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    private UsageSubject Subject()
    {
        var deviceId = Request.Headers["X-Device-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(deviceId))
            return UsageSubject.ForDevice(deviceId);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return UsageSubject.ForIpFallback(ip);
    }
}
