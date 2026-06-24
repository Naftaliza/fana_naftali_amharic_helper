using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Trial;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Anonymous "try it" endpoints. No authentication, no persistence — results are returned
/// in-memory only. The 3-free-tries limit is enforced on the client.
/// </summary>
[ApiController]
[Route("api/trial")]
[AllowAnonymous]
public class TrialController(IMediator mediator) : ControllerBase
{
    /// <summary>Upload a document and get a generic analysis without signing in or saving anything.</summary>
    [HttpPost("analyze")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Analyze(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file provided." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var result = await mediator.Send(new AnalyzeTrialCommand(ms.ToArray(), file.ContentType));
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
