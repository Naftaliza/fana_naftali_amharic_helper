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
/// nothing is persisted (no user, no document, no analysis row). Category is always generic
/// (auto-detected). Supports multiple pages.
///
/// Subject identifies the caller for server-side metering (see IWalletService) — a client
/// device id if the frontend sent one (lib/deviceId.ts), otherwise a coarser per-IP fallback.
/// This replaces the old client-only lib/trial.ts localStorage counter, which any user could
/// reset by clearing browser storage.
/// </summary>
public record AnalyzeTrialCommand(IReadOnlyList<UploadPage> Pages, UsageSubject Subject)
    : IRequest<Result<DocumentAnalysisResult>>;

public class AnalyzeTrialHandler(IOcrProvider ocr, IAiProvider ai, IWalletService wallet)
    : IRequestHandler<AnalyzeTrialCommand, Result<DocumentAnalysisResult>>
{
    public async Task<Result<DocumentAnalysisResult>> Handle(AnalyzeTrialCommand cmd, CancellationToken ct)
    {
        if (cmd.Pages.Count == 0)
            return Result<DocumentAnalysisResult>.Fail("No pages provided.");

        // Charged before OCR/analysis run, same reasoning as AnalyzeDocumentHandler: the calls
        // below are billed regardless of what the client ends up seeing.
        if (!await wallet.TryConsumeAsync(cmd.Subject, "analyze", null, ct))
            return Result<DocumentAnalysisResult>.Fail(WalletErrors.OutOfCredits);

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

/// <summary>Anonymous trial text-to-speech. Builds the spoken text from the supplied analysis.
/// Section defaults to the whole walkthrough. No cache exists for trial audio (nothing is
/// persisted), so every call is metered.</summary>
public record SpeakTrialQuery(DocumentAnalysisResult Analysis, Language Language, UsageSubject Subject, SpokenSection Section = SpokenSection.Full)
    : IRequest<Result<TtsAudio>>;

public class SpeakTrialHandler(ITtsProvider tts, IWalletService wallet) : IRequestHandler<SpeakTrialQuery, Result<TtsAudio>>
{
    public async Task<Result<TtsAudio>> Handle(SpeakTrialQuery q, CancellationToken ct)
    {
        var text = SpokenTextBuilder.Build(q.Analysis, q.Language, q.Section);
        if (string.IsNullOrWhiteSpace(text))
            return Result<TtsAudio>.Fail("Nothing to read.");

        if (!await wallet.TryConsumeAsync(q.Subject, "tts", null, ct))
            return Result<TtsAudio>.Fail(WalletErrors.OutOfCredits);

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
