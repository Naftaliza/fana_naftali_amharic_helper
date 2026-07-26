using AmharicHelper.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AmharicHelper.UnitTests;

public class AnthropicHttpTests
{
    /// <summary>Returns canned responses/exceptions in order, one per SendAsync call.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _script;
        public int Calls { get; private set; }

        public ScriptedHandler(params Func<HttpResponseMessage>[] script) => _script = new(script);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_script.Dequeue()());
        }
    }

    private static HttpResponseMessage Status(int code) => new((System.Net.HttpStatusCode)code);
    private static Func<HttpRequestMessage> Req() => () => new HttpRequestMessage(HttpMethod.Post, "https://example.test/");

    [Fact]
    public async Task Succeeds_on_first_attempt_without_retrying()
    {
        var handler = new ScriptedHandler(() => Status(200));
        var http = new HttpClient(handler);

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default);

        Assert.Equal(1, handler.Calls);
        Assert.True(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Retries_transient_status_then_succeeds()
    {
        var handler = new ScriptedHandler(() => Status(503), () => Status(200));
        var http = new HttpClient(handler);

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 4);

        Assert.Equal(2, handler.Calls);
        Assert.True(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Non_transient_status_returns_immediately()
    {
        var handler = new ScriptedHandler(() => Status(400));
        var http = new HttpClient(handler);

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 4);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(400, (int)resp.StatusCode);
    }

    [Fact]
    public async Task Exhausting_retries_on_transient_status_returns_the_last_response()
    {
        var handler = new ScriptedHandler(() => Status(503), () => Status(503), () => Status(503));
        var http = new HttpClient(handler);

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 3);

        Assert.Equal(3, handler.Calls);
        Assert.Equal(503, (int)resp.StatusCode);
    }

    [Fact]
    public async Task Transport_exception_is_retried_then_succeeds()
    {
        var handler = new ScriptedHandler(
            () => throw new HttpRequestException("connection reset"),
            () => Status(200));
        var http = new HttpClient(handler);

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 4);

        Assert.Equal(2, handler.Calls);
        Assert.True(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Transport_exception_exhausting_all_attempts_throws()
    {
        var handler = new ScriptedHandler(
            () => throw new HttpRequestException("down"),
            () => throw new HttpRequestException("still down"));
        var http = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 2));

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_swallowed_as_a_retryable_timeout()
    {
        using var cts = new System.Threading.CancellationTokenSource();
        var handler = new ScriptedHandler(() =>
        {
            cts.Cancel();
            throw new TaskCanceledException("caller cancelled", null, cts.Token);
        });
        var http = new HttpClient(handler);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, cts.Token, maxAttempts: 4));

        Assert.Equal(1, handler.Calls); // must not retry a real cancellation
    }

    [Fact]
    public async Task Repeated_full_duration_timeout_gives_up_before_maxAttempts()
    {
        // http.Timeout is tiny, so every instantly-thrown failure below reads as having consumed
        // "close to the full timeout" — simulating a call that reliably takes as long as the
        // configured budget, not a quick network blip.
        var handler = new ScriptedHandler(
            () => throw new HttpRequestException("timed out"),
            () => throw new HttpRequestException("timed out"),
            () => throw new HttpRequestException("timed out"),
            () => throw new HttpRequestException("timed out"));
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(50) };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 4));

        // Gives up after 2 full-duration timeouts even though maxAttempts allows 4 — retrying an
        // identical call that deterministically eats its own timeout wouldn't change the outcome.
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Honors_RetryAfter_seconds_header_over_exponential_backoff()
    {
        var resp503 = Status(503);
        resp503.Headers.Add("Retry-After", "0"); // 0s so the test doesn't actually wait
        var handler = new ScriptedHandler(() => resp503, () => Status(200));
        var http = new HttpClient(handler);

        using var resp = await AnthropicHttp.SendWithRetryAsync(http, Req(), NullLogger.Instance, default, maxAttempts: 4);

        Assert.Equal(2, handler.Calls);
        Assert.True(resp.IsSuccessStatusCode);
    }
}
