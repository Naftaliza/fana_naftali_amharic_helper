using System.Net.Http.Json;
using AmharicHelper.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AmharicHelper.Infrastructure.Email;

/// <summary>
/// Sends outbound email via SendGrid's HTTPS Web API (v3 /mail/send) rather than raw SMTP.
/// Railway (this app's production host) blocks outbound SMTP ports (25/465/587) on its network,
/// so MailKit/SmtpEmailSender times out connecting from a Railway container even with correct
/// SendGrid credentials — this reaches the same SendGrid relay over HTTPS (443), which isn't
/// blocked. Reuses EmailOptions: Password holds the SendGrid API key, From the verified sender —
/// Host/Port/Username are SmtpEmailSender-only and unused here.
/// </summary>
public class SendGridApiEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var opts = options.Value;
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Content = JsonContent.Create(new
            {
                personalizations = new[] { new { to = new[] { new { email = message.ToEmail } } } },
                from = new { email = opts.From },
                subject = message.Subject,
                content = new[] { new { type = "text/plain", value = message.Body } },
                attachments = message.Attachments?.Select(a => new
                {
                    content = Convert.ToBase64String(a.Content),
                    filename = a.FileName,
                    type = a.ContentType,
                }).ToArray(),
            }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.Password);

        using var resp = await http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"SendGrid API request failed: {(int)resp.StatusCode} {resp.StatusCode} — {body}");
        }
    }
}
