using System.Globalization;
using System.Text;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Tts;

/// <summary>
/// Builds a full spoken walkthrough of an analysis in the listener's language,
/// grounded entirely in the stored analysis. Reading order:
/// summary → explanation → key points → required actions → deadlines.
/// </summary>
public static class SpokenTextBuilder
{
    private record Labels(string Summary, string Explanation, string KeyPoints, string Actions, string Deadlines, string Required);

    private static Labels LabelsFor(Language language) => language switch
    {
        Language.Amharic => new("ማጠቃለያ", "ዝርዝር ማብራሪያ", "ቁልፍ ነጥቦች", "የሚያስፈልጉ እርምጃዎች", "አስፈላጊ ቀኖች", "ግዴታ"),
        Language.English => new("Summary", "Detailed explanation", "Key points", "Required actions", "Important dates", "Required"),
        _ => new("תקציר", "הסבר מפורט", "נקודות עיקריות", "פעולות נדרשות", "מועדים חשובים", "חובה"),
    };

    private static CultureInfo CultureFor(Language language) => language switch
    {
        Language.Amharic => new CultureInfo("am-ET"),
        Language.English => new CultureInfo("en-US"),
        _ => new CultureInfo("he-IL"),
    };

    /// <summary>Overload for the analysis DTO (used by the anonymous trial flow, which has no entity).</summary>
    public static string Build(DocumentAnalysisResult analysis, Language language, SpokenSection section = SpokenSection.Full) =>
        Build(new DocumentAnalysis
        {
            Summary = analysis.Summary,
            Explanation = analysis.Explanation,
            KeyPoints = analysis.KeyPoints,
            RequiredActions = analysis.RequiredActions.Select(a => new RequiredAction(a.Description, a.IsMandatory)).ToList(),
            Deadlines = analysis.Deadlines.Select(d => new Deadline(d.Date, d.Description)).ToList()
        }, language, section);

    /// <summary>
    /// Builds the spoken text for one passage, or the whole walkthrough when
    /// <paramref name="section"/> is <see cref="SpokenSection.Full"/> (the default) — lets a
    /// per-card "read just the actions" button reuse the exact same wording as the full playback.
    /// </summary>
    public static string Build(DocumentAnalysis analysis, Language language, SpokenSection section = SpokenSection.Full)
    {
        var l = LabelsFor(language);
        var culture = CultureFor(language);
        var sb = new StringBuilder();
        var full = section == SpokenSection.Full;

        // Everything below is read in the listener's single language — no code-switching.
        // Summary and explanation are two distinct passages, each spoken under its own label,
        // so the card the listener is looking at ("Summary") always matches what's being read.
        if (full || section == SpokenSection.Summary)
        {
            var summary = analysis.Summary.For(language);
            if (!string.IsNullOrWhiteSpace(summary))
                sb.Append($"{l.Summary}. {summary}. ");
        }

        if (full || section == SpokenSection.Explanation)
        {
            var explanation = analysis.Explanation.For(language);
            if (!string.IsNullOrWhiteSpace(explanation))
                sb.Append($"{l.Explanation}. {explanation}. ");
        }

        if ((full || section == SpokenSection.KeyPoints) && analysis.KeyPoints.Count > 0)
            sb.Append($"{l.KeyPoints}. {string.Join(". ", analysis.KeyPoints.Select(p => p.For(language)))}. ");

        if ((full || section == SpokenSection.Actions) && analysis.RequiredActions.Count > 0)
        {
            var actions = analysis.RequiredActions.Select(a =>
            {
                var desc = a.Description.For(language);
                return a.IsMandatory ? $"{l.Required}: {desc}" : desc;
            });
            sb.Append($"{l.Actions}. {string.Join(". ", actions)}. ");
        }

        if ((full || section == SpokenSection.Deadlines) && analysis.Deadlines.Count > 0)
        {
            var deadlines = analysis.Deadlines.Select(d =>
            {
                var date = d.Date?.ToString("d", culture) ?? "";
                return $"{date} {d.Description.For(language)}".Trim();
            });
            sb.Append($"{l.Deadlines}. {string.Join(". ", deadlines)}.");
        }

        return sb.ToString().Trim();
    }
}
