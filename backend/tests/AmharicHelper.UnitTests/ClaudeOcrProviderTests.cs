using System.Net;
using AmharicHelper.Infrastructure.Ai;
using AmharicHelper.Infrastructure.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AmharicHelper.UnitTests;

public class ClaudeOcrProviderTests
{
    /// <summary>Returns one canned HTTP response, whatever the status code.</summary>
    private sealed class ScriptedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static ClaudeOcrProvider MakeProvider(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new AiOptions { AnthropicApiKey = "test-key" });
        return new ClaudeOcrProvider(http, options, NullLogger<ClaudeOcrProvider>.Instance);
    }

    [Fact]
    public async Task Usage_limit_response_is_reported_as_temporarily_unavailable()
    {
        var handler = new ScriptedHandler(HttpStatusCode.BadRequest, """
            {"type":"error","error":{"type":"invalid_request_error",
            "message":"You have reached your specified API usage limits. You will regain access on 2026-08-01 at 00:00 UTC."}}
            """);
        var provider = MakeProvider(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExtractTextAsync(new byte[] { 1 }, "image/jpeg"));

        Assert.Contains("temporarily unavailable", ex.Message);
    }

    [Fact]
    public async Task Unauthorized_response_is_reported_as_temporarily_unavailable()
    {
        var handler = new ScriptedHandler(HttpStatusCode.Unauthorized, """
            {"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}
            """);
        var provider = MakeProvider(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExtractTextAsync(new byte[] { 1 }, "image/jpeg"));

        Assert.Contains("temporarily unavailable", ex.Message);
    }

    [Fact]
    public async Task Non_account_level_bad_request_keeps_the_plain_ocr_failed_message()
    {
        // A malformed request that isn't a quota/auth/outage problem — page-specific, not
        // something that would fail identically for every other page or document.
        var handler = new ScriptedHandler(HttpStatusCode.BadRequest, """
            {"type":"error","error":{"type":"invalid_request_error","message":"messages.0.content.0.image.source.base64.data: invalid base64 data"}}
            """);
        var provider = MakeProvider(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExtractTextAsync(new byte[] { 1 }, "image/jpeg"));

        Assert.DoesNotContain("temporarily unavailable", ex.Message);
        Assert.Contains("OCR failed: 400", ex.Message);
    }
}
