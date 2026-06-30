using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.Documents;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Tts;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Trial;

/// <summary>
/// Anonymous "try it" analysis. Runs OCR + AI entirely in memory and returns the result —
/// nothing is persisted (no user, no document, no analysis row). The 3-try limit is enforced
/// on the client. Category is always generic (auto-detected). Supports multiple pages.
/// </summary>
public record AnalyzeTrialCommand(IReadOnlyList<UploadPage> Pages) : IRequest<Result<DocumentAnalysisResult>>;

public class AnalyzeTrialHandler(IOcrProvider ocr, IAiProvider ai)
    : IRequestHandler<AnalyzeTrialCommand, Result<DocumentAnalysisResult>>
{
    public async Task<Result<DocumentAnalysisResult>> Handle(AnalyzeTrialCommand cmd, CancellationToken ct)
    {
        if (cmd.Pages.Count == 0)
            return Result<DocumentAnalysisResult>.Fail("No pages provided.");

        MultiPageOcr.Result ocrResult;
        try
        {
            ocrResult = await MultiPageOcr.RunAsync(
                cmd.Pages.Select(p => (p.Content, p.ContentType)).ToList(), ocr, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentAnalysisResult>.Fail(ex.Message);
        }

        if (ocrResult.KeptPageIndices.Count == 0)
            return Result<DocumentAnalysisResult>.Fail("No readable text was found in the document.");

        var ocrText = ocrResult.CombinedText;

        try
        {
            var result = await ai.AnalyzeAsync(ocrText, DocumentCategory.Other, ct);
            return Result<DocumentAnalysisResult>.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DocumentAnalysisResult>.Fail(ex.Message);
        }
    }
}

/// <summary>Anonymous trial text-to-speech. Builds the spoken text from the supplied analysis.</summary>
public record SpeakTrialQuery(DocumentAnalysisResult Analysis, Language Language) : IRequest<Result<TtsAudio>>;

public class SpeakTrialHandler(ITtsProvider tts) : IRequestHandler<SpeakTrialQuery, Result<TtsAudio>>
{
    public async Task<Result<TtsAudio>> Handle(SpeakTrialQuery q, CancellationToken ct)
    {
        var text = SpokenTextBuilder.Build(q.Analysis, q.Language);
        if (string.IsNullOrWhiteSpace(text))
            return Result<TtsAudio>.Fail("Nothing to read.");

        try
        {
            var audio = await tts.SynthesizeAsync(text, q.Language, ct);
            return Result<TtsAudio>.Ok(audio);
        }
        catch (InvalidOperationException ex)
        {
            return Result<TtsAudio>.Fail(ex.Message);
        }
    }
}
