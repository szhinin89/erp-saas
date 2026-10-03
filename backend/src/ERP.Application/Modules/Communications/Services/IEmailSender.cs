namespace ERP.Application.Modules.Communications.Services;

public interface IEmailSender
{
    /// <summary>
    /// Envía el mensaje. Debe respetar <paramref name="ct"/> y <see cref="CommunicationEmailSettings.SmtpTimeout"/>;
    /// un timeout se informa como <see cref="TimeoutException"/>.
    /// </summary>
    Task SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default);
}

/// <param name="CommunicationId">
/// Identidad estable de la comunicación (<c>CommunicationOutbox.Id</c>): el transporte deriva de ella
/// un Message-ID idéntico en todos los reintentos. Null para envíos fuera de la outbox (correo de prueba).
/// </param>
public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string? BodyHtml,
    string? BodyText,
    IReadOnlyCollection<EmailAttachment> Attachments,
    Guid? CommunicationId = null
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
    /// <summary>Timeout de un envío; siempre menor que el lease (<see cref="CommunicationDeliveryTiming"/>).</summary>
    public TimeSpan SmtpTimeout { get; init; } = CommunicationDeliveryTiming.DefaultSmtpTimeout;

    public bool CanSend =>
        Enabled
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && SmtpPort > 0
        && !string.IsNullOrWhiteSpace(SenderEmail);
}
