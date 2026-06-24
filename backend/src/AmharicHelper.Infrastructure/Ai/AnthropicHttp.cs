using System.Net;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Ai;

/// <summary>
/// Shared helper for calling the Anthropic API with retry on transient failures
/// (429 Too Many Requests, 529 Overloaded, 500/503). Each attempt rebuilds the request
/// because an <see cref="HttpRequestMessage"/> can only be sent once.
/// </summary>
public static class AnthropicHttp
{
    private static readonly HashSet<int> Transient = [429, 500, 503, 529];

    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient http,
        Func<HttpRequestMessage> requestFactory,
        ILogger logger,
        CancellationToken ct,
        int maxAttempts = 4)
    {
        HttpResponseMessage? resp = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            resp?.Dispose();
            resp = await http.SendAsync(requestFactory(), ct);
            if (resp.IsSuccessStatusCode || !Transient.Contains((int)resp.StatusCode))
                return resp;

            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)); // 0.5s, 1s, 2s
                logger.LogWarning("Anthropic transient {Status}; retry {Attempt}/{Max} in {Delay}ms",
                    (int)resp.StatusCode, attempt, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
        }
        return resp!;
    }
}
