using AmharicHelper.Application.Prompts;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class PromptTemplatesTests
{
    // Analysis is delivered through a forced tool call, so the tool's input_schema is the single
    // source of truth for the output shape. A JSON skeleton in the prompt on top of that measurably
    // breaks structured generation — the model starts hand-writing JSON into the tool's fields and
    // emits arrays as (often malformed) JSON strings. See the comment on AnalysisSystemBase for the
    // measurements. These tests are the guard against that regressing back in.

    public static TheoryData<DocumentCategory> AllCategories() =>
    [
        DocumentCategory.Government, DocumentCategory.Bank, DocumentCategory.Insurance,
        DocumentCategory.Employment, DocumentCategory.Healthcare, DocumentCategory.Municipality,
        DocumentCategory.Other,
    ];

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Analysis_prompt_does_not_tell_the_model_to_emit_raw_json(DocumentCategory category)
    {
        var prompt = PromptTemplates.BuildAnalysisPrompt(category);

        Assert.DoesNotContain("Return ONLY a JSON object", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no markdown, no commentary", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Analysis_prompt_does_not_embed_a_json_field_skeleton(DocumentCategory category)
    {
        var prompt = PromptTemplates.BuildAnalysisPrompt(category);

        // The skeleton's give-away: a quoted schema key immediately followed by a JSON object or
        // array literal (e.g. `"keyPoints": [{ "he": "" ...`). The LANGUAGE section legitimately
        // shows the { "he": "", "am": "", "en": "" } value shape once, so key names are what we
        // assert on, not braces in general.
        foreach (var key in new[] { "summary", "documentType", "urgencyLevel", "keyPoints", "requiredActions", "deadlines", "explanation" })
        {
            Assert.DoesNotContain($"\"{key}\": {{", prompt);
            Assert.DoesNotContain($"\"{key}\": [", prompt);
        }
    }

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Analysis_prompt_keeps_the_safety_and_language_guardrails(DocumentCategory category)
    {
        var prompt = PromptTemplates.BuildAnalysisPrompt(category);

        // Removing the skeleton must not have taken any semantic guidance with it.
        Assert.Contains("GROUNDING", prompt);
        Assert.Contains("NEVER invent dates", prompt);
        Assert.Contains("never interpret medical results", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LANGUAGE:", prompt);
        Assert.Contains("summary", prompt);
        Assert.Contains("category", prompt);
        Assert.Contains("explanation", prompt);
        // Category-specific guidance is what drives requiredActions/deadlines being populated.
        Assert.Contains("DOCUMENT CONTEXT:", prompt);
    }
}
