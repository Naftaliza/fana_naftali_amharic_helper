using System.Text;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.Tts;

/// <summary>
/// Builds a full spoken walkthrough of an analysis in the listener's language,
/// grounded entirely in the stored analysis. Reading order:
/// explanation → key points → required actions → deadlines.
/// </summary>
public static class SpokenTextBuilder
{
    private record Labels(string Summary, string KeyPoints, string Actions, string Deadlines);

    private static Labels LabelsFor(Language language) => language switch
    {
        Language.Amharic => new("ማጠቃለያ", "ቁልፍ ነጥቦች", "የሚያስፈልጉ እርምጃዎች", "አስፈላጊ ቀኖች"),
        Language.English => new("Summary", "Key points", "Required actions", "Important dates"),
        _ => new("תקציר", "נקודות עיקריות", "פעולות נדרשות", "מועדים חשובים"),
    };

    /// <summary>Overload for the analysis DTO (used by the anonymous trial flow, which has no entity).</summary>
    public static string Build(DocumentAnalysisResult analysis, Language language) =>
        Build(new DocumentAnalysis
        {
            Summary = analysis.Summary,
            Explanation = analysis.Explanation,
            KeyPoints = analysis.KeyPoints,
            RequiredActions = analysis.RequiredActions.Select(a => new RequiredAction(a.Description, a.IsMandatory)).ToList(),
            Deadlines = analysis.Deadlines.Select(d => new Deadline(d.Date, d.Description)).ToList()
        }, language);

    public static string Build(DocumentAnalysis analysis, Language language)
    {
        var l = LabelsFor(language);
        var sb = new StringBuilder();

        // Everything below is read in the listener's single language — no code-switching.
        var explanation = analysis.Explanation.For(language);
        if (string.IsNullOrWhiteSpace(explanation))
            explanation = analysis.Summary.For(language);
        if (!string.IsNullOrWhiteSpace(explanation))
            sb.Append($"{l.Summary}. {explanation}. ");

        if (analysis.KeyPoints.Count > 0)
            sb.Append($"{l.KeyPoints}. {string.Join(". ", analysis.KeyPoints.Select(p => p.For(language)))}. ");

        if (analysis.RequiredActions.Count > 0)
            sb.Append($"{l.Actions}. {string.Join(". ", analysis.RequiredActions.Select(a => a.Description.For(language)))}. ");

        if (analysis.Deadlines.Count > 0)
        {
            var deadlines = analysis.Deadlines.Select(d =>
            {
                var date = d.Date?.ToString("d") ?? "";
                return $"{date} {d.Description.For(language)}".Trim();
            });
            sb.Append($"{l.Deadlines}. {string.Join(". ", deadlines)}.");
        }

        return sb.ToString().Trim();
    }
}
