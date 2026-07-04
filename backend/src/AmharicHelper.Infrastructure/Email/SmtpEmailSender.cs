using AmharicHelper.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AmharicHelper.Infrastructure.Email;

public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}

/// <summary>Sends outbound email over SMTP via MailKit. Configured through the Email:Smtp
/// section (Email__Smtp__* env var overrides), the same double-underscore convention used by
/// Ai:Provider etc.</summary>
public class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var opts = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(opts.From));
        mime.To.Add(MailboxAddress.Parse(message.ToEmail));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder { TextBody = message.Body };
        foreach (var a in message.Attachments ?? Array.Empty<EmailAttachment>())
            builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(opts.Host, opts.Port, SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(opts.Username))
            await client.AuthenticateAsync(opts.Username, opts.Password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
