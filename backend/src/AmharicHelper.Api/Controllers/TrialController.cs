using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Trial;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Anonymous "try it" endpoints. No authentication, no persistence — results are returned
/// in-memory only. The 3-free-tries limit is enforced on the client.
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

        var result = await mediator.Send(new AnalyzeTrialCommand(pages!));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Read a trial analysis aloud (MP3) in the requested language.</summary>
    [HttpPost("speech")]
    public async Task<IActionResult> Speech(TrialSpeechRequest request)
    {
        var result = await mediator.Send(new SpeakTrialQuery(request.Analysis, request.Language));
        if (!result.Success || result.Value is null)
            return BadRequest(new { error = result.Error });
        return File(result.Value.Content, result.Value.ContentType);
    }
}
