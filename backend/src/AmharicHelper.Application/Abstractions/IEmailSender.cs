namespace AmharicHelper.Application.Abstractions;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

public record EmailMessage(
    string ToEmail,
    string Subject,
    string Body,
    IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>Abstraction over outbound email. The MVP implementation is SmtpEmailSender (MailKit),
/// config-driven like IAiProvider/IOcrProvider/ITtsProvider. Deliberately minimal — no templating
/// engine, no HTML body composition, since the only current caller (invoice generation) sends a
/// short plain-text body with a PDF attachment.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
