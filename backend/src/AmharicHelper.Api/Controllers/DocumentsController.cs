using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Chat;
using AmharicHelper.Application.Features.Documents;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

[Authorize]
[EnableRateLimiting("documents")]
public class DocumentsController(IMediator mediator) : ApiControllerBase(mediator)
{
    /// <summary>
    /// Maximum pages per uploaded document. Bounds OCR cost/latency per request and the stored
    /// file count. Kept generous enough for a long multi-page letter.
    /// </summary>
    private const int MaxPages = 10;

    /// <summary>
    /// Upload a document of one or more pages (PDF/JPG/PNG). Pages are saved immediately; OCR then
    /// runs in the background (see DocumentProcessor). Returns 202 with the document id right away
    /// — poll GET /documents/{id} (Status/ProcessedPages/TotalPages) for progress and completion.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        var (pages, error) = await UploadValidation.BuildPagesAsync(files, MaxPages, HttpContext.RequestAborted);
        if (error is not null) return BadRequest(new { error });

        var result = await Mediator.Send(new UploadDocumentCommand(CurrentUserId, pages!));
        if (!result.Success) return BadRequest(new { error = result.Error });
        return AcceptedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await Mediator.Send(new ListDocumentsQuery(CurrentUserId));
        return Ok(result.Value);
    }

    /// <summary>Saves a previously-computed anonymous trial analysis to the now-authenticated
    /// user's account. There is no OCR text or page file for a trial result — only the analysis
    /// itself is persisted.</summary>
    [HttpPost("attach-trial")]
    public async Task<IActionResult> AttachTrial(DocumentAnalysisResult analysis)
    {
        var result = await Mediator.Send(new AttachTrialAnalysisCommand(CurrentUserId, analysis));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await Mediator.Send(new GetDocumentQuery(CurrentUserId, id));
        return result.Success ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await Mediator.Send(new DeleteDocumentCommand(CurrentUserId, id));
        return result.Success ? NoContent() : NotFound(new { error = result.Error });
    }

    /// <summary>Run AI analysis on a document. Category selects the prompt template.</summary>
    [HttpPost("{id:guid}/analyze")]
    public async Task<IActionResult> Analyze(Guid id, [FromQuery] DocumentCategory category = DocumentCategory.Other)
    {
        var result = await Mediator.Send(new AnalyzeDocumentCommand(CurrentUserId, id, category));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Retry OCR on a document whose processing previously failed. Resets it to Pending
    /// and re-queues it on the same background pipeline as a fresh upload.</summary>
    [HttpPost("{id:guid}/retry-ocr")]
    public async Task<IActionResult> RetryOcr(Guid id)
    {
        var result = await Mediator.Send(new RetryOcrCommand(CurrentUserId, id));
        return result.Success ? Ok(new { ok = true }) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Fetch one uploaded page's raw file (image or PDF), so the user can see what they
    /// photographed alongside the AI's analysis. Index is 0-based. Content is immutable once
    /// uploaded, so it's safe to cache in the browser.
    /// </summary>
    [HttpGet("{id:guid}/pages/{index:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Page(Guid id, int index)
    {
        var result = await Mediator.Send(new GetDocumentPageQuery(CurrentUserId, id, index));
        if (!result.Success || result.Value is null)
            return NotFound(new { error = result.Error });
        return File(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>Synthesize spoken audio (MP3) of the document's analysis in the given language.
    /// Section defaults to the whole walkthrough; pass e.g. Actions for a per-card "read just
    /// this" button.</summary>
    [HttpGet("{id:guid}/speech")]
    public async Task<IActionResult> Speech(
        Guid id, [FromQuery] Language language = Language.Hebrew, [FromQuery] SpokenSection section = SpokenSection.Full)
    {
        var result = await Mediator.Send(new SpeakDocumentQuery(CurrentUserId, id, language, section));
        if (!result.Success || result.Value is null)
            return BadRequest(new { error = result.Error });
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpGet("{id:guid}/chat")]
    public async Task<IActionResult> ChatHistory(Guid id)
    {
        var result = await Mediator.Send(new GetChatHistoryQuery(CurrentUserId, id));
        return result.Success ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<IActionResult> Chat(Guid id, SendChatRequest request)
    {
        var result = await Mediator.Send(
            new SendChatMessageCommand(CurrentUserId, id, request.Question, request.ResponseLanguage));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
