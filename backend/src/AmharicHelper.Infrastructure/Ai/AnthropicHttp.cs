using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// Shared helper for calling the Anthropic API with retry on transient failures: status codes
/// (429, 500, 502, 503, 504, 529) AND transport-level failures (connection reset, DNS, or this
/// client's own HttpClient.Timeout firing) — the latter used to escape retry entirely and surface
/// as an unhandled 500, despite being one of the most common real-world failure modes. Each
/// attempt rebuilds the request because an <see cref="HttpRequestMessage"/> can only be sent once.
/// </summary>
public static class AnthropicHttp
{
    private static readonly HashSet<int> Transient = [429, 500, 502, 503, 504, 529];

    // How many attempts are allowed to fail by burning the *entire* HttpClient.Timeout before
    // giving up early, instead of continuing on to maxAttempts. A quick transport failure (DNS,
    // connection reset) is cheap to retry several times over; a failure that took the full
    // timeout means the call itself is just slow, and retrying the identical request will
    // predictably take just as long again — multiplying the wait without changing the outcome.
    private const int MaxSlowTimeouts = 2;

    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient http,
        Func<HttpRequestMessage> requestFactory,
        ILogger logger,
        CancellationToken ct,
        int maxAttempts = 4)
    {
        HttpResponseMessage? resp = null;
        var slowTimeouts = 0;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            resp?.Dispose();
            Exception? transportError = null;
            var sw = Stopwatch.StartNew();
            try
            {
                resp = await http.SendAsync(requestFactory(), ct);
            }
            // A TaskCanceledException with ct NOT itself cancelled means HttpClient.Timeout fired,
            // not the caller — that's a transient timeout, not a real cancellation, so retry it.
            // If ct.IsCancellationRequested is true, the `when` guard is false and this rethrows.
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
            {
                transportError = ex;
            }
            sw.Stop();

            if (transportError is null && (resp!.IsSuccessStatusCode || !Transient.Contains((int)resp.StatusCode)))
                return resp;

            // Elapsed time close to the client's configured timeout means the timeout is what
            // actually ended the attempt (a genuinely fast failure — DNS, connection reset —
            // finishes in milliseconds, nowhere near it).
            var isSlowTimeout = transportError is not null && sw.Elapsed >= http.Timeout - TimeSpan.FromSeconds(2);
            if (isSlowTimeout) slowTimeouts++;

            var giveUp = attempt >= maxAttempts || slowTimeouts >= MaxSlowTimeouts;
            if (!giveUp)
            {
                var delay = RetryDelay(resp, attempt);
                logger.LogWarning(transportError,
                    "Anthropic {Outcome}; retry {Attempt}/{Max} in {Delay}ms",
                    transportError is null ? $"transient {(int)resp!.StatusCode}" : isSlowTimeout ? "timeout" : "transport failure",
                    attempt, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                continue;
            }

            // Out of attempts (or gave up early on a repeated full-duration timeout). A transient
            // status response is returned for the caller to inspect; a transport failure never
            // produced a response at all, so it must be thrown instead.
            if (transportError is not null) throw transportError;
            break;
        }
        return resp!;
    }

    // Honors the server's Retry-After header on a 429 (seconds or an HTTP-date) instead of always
    // guessing with exponential backoff — avoids hammering straight back into the same rate limit.
    private static TimeSpan RetryDelay(HttpResponseMessage? resp, int attempt)
    {
        var retryAfter = resp?.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta) return delta;
        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }
        return TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)); // 0.5s, 1s, 2s
    }
}
