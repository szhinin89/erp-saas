using ERP.Application.Modules.Communications.Services;
using System.Net;
using System.Net.Mail;

namespace ERP.Infrastructure.Communications;

public sealed class SmtpEmailSender : IEmailSender
{
    public async Task SendAsync(
        EmailMessage message,
        CommunicationEmailSettings settings,
        CancellationToken ct = default
    )
    {
        if (!settings.CanSend)
            throw new InvalidOperationException("La configuración SMTP de Communications está incompleta o inactiva.");

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail!, settings.SenderName),
            Subject = message.Subject,
            Body = message.BodyHtml ?? message.BodyText ?? string.Empty,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.BodyHtml),
        };

        mail.To.Add(new MailAddress(message.ToEmail, message.ToName));
        if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail))
            mail.ReplyToList.Add(new MailAddress(settings.ReplyToEmail));

        foreach (var attachment in message.Attachments)
            mail.Attachments.Add(CreateAttachment(attachment));

        using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
        };

        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
            client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);

        await client.SendMailAsync(mail, ct);
    }

    private static Attachment CreateAttachment(EmailAttachment attachment)
    {
        if (attachment.BinaryContent is { Length: > 0 })
        {
            var stream = new MemoryStream(attachment.BinaryContent);
            return new Attachment(stream, attachment.FileName, attachment.ContentType);
        }

        if (string.IsNullOrWhiteSpace(attachment.FileStoragePath) || !File.Exists(attachment.FileStoragePath))
            throw new FileNotFoundException("No se encontró el archivo adjunto de la comunicación.", attachment.FileStoragePath);

        return new Attachment(attachment.FileStoragePath, attachment.ContentType);
    }
}
