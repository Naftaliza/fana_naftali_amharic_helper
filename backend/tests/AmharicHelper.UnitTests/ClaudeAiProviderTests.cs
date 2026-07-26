using System.Net;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AmharicHelper.UnitTests;

public class ClaudeAiProviderTests
{
    /// <summary>Returns canned Anthropic Messages API responses in order, one per HTTP call.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<string> _bodies;
        public int Calls { get; private set; }

        public ScriptedHandler(params string[] bodies) => _bodies = new(bodies);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_bodies.Dequeue())
            });
        }
    }

    // A minimal Anthropic tool_use response wrapping the given raw `input` object text.
    private static string ToolUseResponse(string inputJson) => $$"""
        {
          "content": [{ "type": "tool_use", "id": "toolu_1", "name": "submit_analysis", "input": {{inputJson}} }],
          "stop_reason": "tool_use",
          "usage": { "input_tokens": 10, "output_tokens": 10 }
        }
        """;

    private static ClaudeAiProvider MakeProvider(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new AiOptions { AnthropicApiKey = "test-key" });
        return new ClaudeAiProvider(http, options, NullLogger<ClaudeAiProvider>.Instance);
    }

    [Fact]
    public async Task Succeeds_on_first_attempt_when_the_json_is_well_formed()
    {
        var handler = new ScriptedHandler(ToolUseResponse("""{ "requiredActions": [] }"""));
        var provider = MakeProvider(handler);

        var result = await provider.AnalyzeAsync("some document text", DocumentCategory.Other);

        Assert.Empty(result.RequiredActions);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Retries_once_when_the_first_generation_is_unparseable_then_succeeds()
    {
        // First "generation" comes back with genuinely unparseable content for requiredActions
        // (simulating the real, observed failure: Claude occasionally corrupts the escaping when
        // double-encoding an array field as a JSON string). The second, fresh generation is clean.
        var handler = new ScriptedHandler(
            ToolUseResponse("""{ "requiredActions": "not valid json at all {{{" }"""),
            ToolUseResponse("""{ "requiredActions": [] }"""));
        var provider = MakeProvider(handler);

        var result = await provider.AnalyzeAsync("some document text", DocumentCategory.Other);

        Assert.Empty(result.RequiredActions);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Gives_up_after_exhausting_attempts_on_repeated_unparseable_json()
    {
        var handler = new ScriptedHandler(
            ToolUseResponse("""{ "requiredActions": "not valid json at all {{{" }"""),
            ToolUseResponse("""{ "requiredActions": "still not valid {{{" }"""),
            ToolUseResponse("""{ "requiredActions": "still not valid on the last try {{{" }"""));
        var provider = MakeProvider(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.AnalyzeAsync("some document text", DocumentCategory.Other));

        Assert.Contains("unparseable", ex.Message);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Tolerates_requiredActions_double_encoded_as_a_clean_json_string()
    {
        var handler = new ScriptedHandler(ToolUseResponse(
            """{ "requiredActions": "[{\"description\":{\"he\":\"a\",\"am\":\"b\",\"en\":\"c\"},\"isMandatory\":true}]" }"""));
        var provider = MakeProvider(handler);

        var result = await provider.AnalyzeAsync("some document text", DocumentCategory.Other);

        Assert.Single(result.RequiredActions);
        Assert.Equal(1, handler.Calls);
    }
}
