namespace ERP.Application.Modules.Communications.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default);
}

public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string? BodyHtml,
    string? BodyText,
    IReadOnlyCollection<EmailAttachment> Attachments
);

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    string? FileStoragePath,
    byte[]? BinaryContent
);

public sealed record CommunicationEmailSettings(
    bool Enabled,
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string? SenderEmail,
    string? SenderName,
    bool UseSsl,
    string? ReplyToEmail,
    int MaxRetries,
    string DefaultLanguage
)
{
    public bool CanSend =>
        Enabled
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && SmtpPort > 0
        && !string.IsNullOrWhiteSpace(SenderEmail);
}
