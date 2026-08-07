using System.Net;
using System.Text;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AmharicHelper.UnitTests;

public class AzureSttProviderTests
{
    /// <summary>Returns one canned response and captures the outgoing request for inspection —
    /// same pattern as AnthropicHttpTests.ScriptedHandler.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;
        // The provider disposes its MultipartFormDataContent (a `using`) right after SendAsync
        // returns, so the body must be captured here, during the call, not read back afterward.
        public string? RequestBody { get; private set; }

        public CapturingHandler(Func<HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Content is not null) RequestBody = await request.Content.ReadAsStringAsync(ct);
            return _respond();
        }
    }

    private static AzureSttProvider MakeProvider(HttpMessageHandler handler, string key = "test-key") =>
        new(new HttpClient(handler),
            Options.Create(new SttOptions { AzureSpeechKey = key, AzureRegion = "eastus" }),
            NullLogger<AzureSttProvider>.Instance);

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task Throws_when_key_is_not_configured()
    {
        var provider = MakeProvider(new CapturingHandler(() => JsonResponse("{}")), key: "");
        using var audio = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.TranscribeAsync(audio, "question.webm", "audio/webm;codecs=opus", Language.Amharic));
    }

    // Regression test for the bug this fix addresses: MediaTypeHeaderValue's single-string
    // constructor does strict RFC 7231 parsing and throws FormatException on exactly what
    // MediaRecorder sends browser-side ("audio/webm;codecs=opus" — no space before the
    // parameter). This exception was previously unhandled and surfaced as a 500 to every real
    // voice-input attempt, before the frontend ever got a chance to show its own error message.
    [Theory]
    [InlineData("audio/webm;codecs=opus")] // Android Chrome
    [InlineData("audio/webm")]
    [InlineData("audio/mp4")]              // iOS Safari
    [InlineData("")]
    public async Task Accepts_every_content_type_MediaRecorder_actually_sends(string contentType)
    {
        var handler = new CapturingHandler(() =>
            JsonResponse("""{"combinedPhrases":[{"text":"שלום"}]}"""));
        var provider = MakeProvider(handler);
        using var audio = new MemoryStream([1, 2, 3]);

        var result = await provider.TranscribeAsync(audio, "question.webm", contentType, Language.Amharic);

        Assert.Equal("שלום", result.Text);
    }

    [Fact]
    public async Task Sends_locales_for_the_requested_language_as_a_JSON_string_field()
    {
        var handler = new CapturingHandler(() => JsonResponse("""{"combinedPhrases":[{"text":"x"}]}"""));
        var provider = MakeProvider(handler);
        using var audio = new MemoryStream([1, 2, 3]);

        await provider.TranscribeAsync(audio, "question.webm", "audio/webm;codecs=opus", Language.Amharic);

        var body = handler.RequestBody!;
        // Amharic pulls in Hebrew too — users code-switch mid-sentence (see SttOptions.LocalesFor).
        Assert.Contains("am-ET", body);
        Assert.Contains("he-IL", body);
        Assert.Contains("name=definition", body);
        Assert.Contains("name=audio", body);
    }

    [Fact]
    public async Task Non_success_status_throws_InvalidOperationException()
    {
        var handler = new CapturingHandler(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("rate limited"),
        });
        var provider = MakeProvider(handler);
        using var audio = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.TranscribeAsync(audio, "question.webm", "audio/webm;codecs=opus", Language.Amharic));
    }

    [Fact]
    public async Task Empty_combinedPhrases_returns_empty_text_without_throwing()
    {
        var handler = new CapturingHandler(() => JsonResponse("""{"combinedPhrases":[]}"""));
        var provider = MakeProvider(handler);
        using var audio = new MemoryStream([1, 2, 3]);

        var result = await provider.TranscribeAsync(audio, "question.webm", "audio/webm;codecs=opus", Language.Amharic);

        Assert.Equal("", result.Text);
    }
}
