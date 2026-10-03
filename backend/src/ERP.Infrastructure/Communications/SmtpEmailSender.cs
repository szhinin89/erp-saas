using ERP.Application.Modules.Communications.Services;
using System.Net;
using System.Net.Mail;

namespace ERP.Infrastructure.Communications;

public sealed class SmtpEmailSender : IEmailSender
{
    public async Task<EmailDeliveryReceipt> SendAsync(
        EmailMessage message,
        CommunicationEmailSettings settings,
        CancellationToken ct = default
    )
    {
        if (!settings.CanSend)
            throw new InvalidOperationException("La configuración SMTP de Communications está incompleta o inactiva.");

        using var mail = BuildMailMessage(message, settings);

        // ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — timeout explícito (< lease del claim).
        // SmtpClient.Timeout solo rige para Send síncrono: SendMailAsync se acota con el token.
        var timeoutMs = (int)settings.SmtpTimeout.TotalMilliseconds;
        using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
            Timeout = timeoutMs,
        };

        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
            client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.SmtpTimeout);
        try
        {
            await client.SendMailAsync(mail, timeout.Token);
            // System.Net.Mail no expone la respuesta del servidor (id de cola del proveedor): null, nunca inventado.
            return EmailDeliveryReceipt.WithoutProviderId;
        }
        // SmtpClient envuelve la cancelación de NUESTRO token de timeout como SmtpException (sin causa
        // interna) u OperationCanceledException según la fase: se decide por el estado de los tokens,
        // no por el texto. Una cancelación del caller (apagado) se propaga tal cual.
        catch (Exception ex)
            when (ex is OperationCanceledException or SmtpException
                && timeout.IsCancellationRequested
                && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"El servidor SMTP no completó el envío en {settings.SmtpTimeout.TotalSeconds:0} s.",
                ex
            );
        }
    }

    /// <summary>
    /// Arma el <see cref="MailMessage"/>. Con <see cref="EmailMessage.CommunicationId"/> fija el
    /// <c>Message-ID</c> de <see cref="CommunicationMessageId"/>: depende solo del Id de la comunicación
    /// (no del remitente ni de ninguna configuración SMTP), así que es idéntico en todos los reintentos,
    /// sin destinatario, tenant ni secretos. No se asume que el servidor SMTP deduplique.
    /// </summary>
    public static MailMessage BuildMailMessage(EmailMessage message, CommunicationEmailSettings settings)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail!, settings.SenderName),
            Subject = message.Subject,
            Body = message.BodyHtml ?? message.BodyText ?? string.Empty,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.BodyHtml),
        };

        try
        {
            mail.To.Add(new MailAddress(message.ToEmail, message.ToName));
            if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail))
                mail.ReplyToList.Add(new MailAddress(settings.ReplyToEmail));

            if (message.CommunicationId is Guid communicationId)
                mail.Headers.Add("Message-ID", CommunicationMessageId.For(communicationId));

            foreach (var attachment in message.Attachments)
                mail.Attachments.Add(CreateAttachment(attachment));

            return mail;
        }
        catch
        {
            mail.Dispose();
            throw;
        }
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
