namespace ERP.Application.Modules.Communications.Services;

public interface IEmailSender
{
    /// <summary>
    /// Envía el mensaje. Debe respetar <paramref name="ct"/> y <see cref="CommunicationEmailSettings.SmtpTimeout"/>;
    /// un timeout se informa como <see cref="TimeoutException"/>.
    /// </summary>
    Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default);
}

/// <summary>
/// Respuesta del proveedor a un envío aceptado. <see cref="ProviderMessageId"/> es el id REAL que el
/// proveedor devolvió (null si no expone ninguno, como System.Net.Mail); nunca se inventa. Es distinto
/// del Message-ID propio y determinístico (<see cref="CommunicationMessageId"/>).
/// </summary>
public sealed record EmailDeliveryReceipt(string? ProviderMessageId)
{
    public static EmailDeliveryReceipt WithoutProviderId { get; } = new((string?)null);
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

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — adjunto YA resuelto (bytes): el transporte nunca lee rutas ni el
/// filesystem; <see cref="ICommunicationAttachmentResolver"/> obtiene el contenido del almacenamiento
/// oficial o del módulo dueño antes de enviar.
/// </summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

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
