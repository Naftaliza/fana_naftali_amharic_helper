using System.Text.Json;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Infrastructure.Ai;
using Xunit;

namespace AmharicHelper.UnitTests;

public class TolerantListConverterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TolerantListConverterFactory() }
    };

    private sealed class Wrapper
    {
        public List<RequiredActionDto> RequiredActions { get; set; } = new();
    }

    [Fact]
    public void Deserializes_a_real_array_normally()
    {
        const string json = """
            { "requiredActions": [{ "description": { "he": "a", "am": "b", "en": "c" }, "isMandatory": true }] }
            """;

        var result = JsonSerializer.Deserialize<Wrapper>(json, Options)!;

        Assert.Single(result.RequiredActions);
        Assert.True(result.RequiredActions[0].IsMandatory);
        Assert.Equal("c", result.RequiredActions[0].Description.En);
    }

    [Fact]
    public void Tolerates_the_array_double_encoded_as_a_json_string()
    {
        // The exact failure mode observed from Claude: the array is emitted as a JSON string
        // containing JSON text, instead of a real array.
        const string json = """
            { "requiredActions": "[{\"description\":{\"he\":\"a\",\"am\":\"b\",\"en\":\"c\"},\"isMandatory\":true}]" }
            """;

        var result = JsonSerializer.Deserialize<Wrapper>(json, Options)!;

        Assert.Single(result.RequiredActions);
        Assert.True(result.RequiredActions[0].IsMandatory);
        Assert.Equal("c", result.RequiredActions[0].Description.En);
    }

    [Fact]
    public void Null_becomes_an_empty_list()
    {
        const string json = """{ "requiredActions": null }""";

        var result = JsonSerializer.Deserialize<Wrapper>(json, Options)!;

        Assert.Empty(result.RequiredActions);
    }

    [Fact]
    public void Empty_string_becomes_an_empty_list()
    {
        const string json = """{ "requiredActions": "" }""";

        var result = JsonSerializer.Deserialize<Wrapper>(json, Options)!;

        Assert.Empty(result.RequiredActions);
    }

    [Fact]
    public void Double_encoded_empty_array_string_becomes_an_empty_list()
    {
        const string json = """{ "requiredActions": "[]" }""";

        var result = JsonSerializer.Deserialize<Wrapper>(json, Options)!;

        Assert.Empty(result.RequiredActions);
    }
}
