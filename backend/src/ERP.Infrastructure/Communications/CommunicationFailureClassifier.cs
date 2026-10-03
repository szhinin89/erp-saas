using ERP.Domain.Modules.Communications.Enums;
using System.Net.Mail;
using System.Net.Sockets;

namespace ERP.Infrastructure.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — clasificación única de un fallo de entrega email
/// (ADR-039 D10). Decide solo con información estructurada (tipo de excepción,
/// <see cref="SmtpStatusCode"/>, <see cref="SocketError"/>), nunca con el texto del mensaje.
/// Lo no reconocido es <see cref="CommunicationFailureCategory.Unknown"/> (reintento conservador),
/// nunca Permanent por defecto.
/// </summary>
public static class CommunicationFailureClassifier
{
    // Respuestas SMTP que delatan configuración (TLS/credenciales/permiso del cliente), no un fallo
    // transitorio ni del destinatario: 454 (TLS no disponible / auth temporal rechazada en este
    // cliente), 530 (requiere STARTTLS/auth), 534/535 (mecanismo/credenciales de auth inválidos).
    private static readonly HashSet<int> ConfigurationCodes = [454, 530, 534, 535];

    // Rechazos definitivos (no mejoran reintentando): buzón inexistente/no permitido, sin relay,
    // tamaño excedido, transacción rechazada.
    private static readonly HashSet<int> PermanentCodes = [550, 551, 552, 553, 554];

    public static CommunicationFailureCategory Classify(Exception exception) =>
        exception switch
        {
            TimeoutException => CommunicationFailureCategory.Transient,
            SmtpFailedRecipientsException many => ClassifyRecipients(many),
            SmtpFailedRecipientException one => ClassifyStatus((int)one.StatusCode),
            SmtpException smtp => ClassifySmtp(smtp),
            FormatException => CommunicationFailureCategory.Permanent, // dirección mal formada
            SocketException socket => ClassifySocket(socket),
            IOException => CommunicationFailureCategory.Transient,
            _ => CommunicationFailureCategory.Unknown,
        };

    /// <summary>
    /// ZH-COMMUNICATIONS-CONTRACT-01 — código del proveedor si el fallo lo trae (p. ej. <c>smtp:550</c>).
    /// </summary>
    public static string? ProviderCode(Exception exception) =>
        exception switch
        {
            SmtpFailedRecipientsException many when many.InnerExceptions.Length > 0 =>
                $"smtp:{(int)many.InnerExceptions[0].StatusCode}",
            SmtpException smtp when smtp.StatusCode != SmtpStatusCode.GeneralFailure => $"smtp:{(int)smtp.StatusCode}",
            _ => null,
        };

    /// <summary>
    /// Texto persistible del fallo (<c>LastError</c> y <c>ErrorSafeText</c>): tipo de error + código del
    /// proveedor. Nunca el mensaje de excepciones externas (los rechazos SMTP incluyen la dirección del
    /// destinatario); sí el de <see cref="TimeoutException"/>, que redacta el propio ERP.
    /// </summary>
    public static string SafeDescription(Exception exception) =>
        exception switch
        {
            TimeoutException timeout => $"{nameof(TimeoutException)}: {timeout.Message}",
            _ when ProviderCode(exception) is { } code => $"{exception.GetType().Name} ({code})",
            _ => exception.GetType().Name,
        };

    private static CommunicationFailureCategory ClassifyRecipients(SmtpFailedRecipientsException many)
    {
        // Todos los destinatarios fallidos: si alguno es transitorio, se reintenta; el email lleva un
        // único "To", así que en la práctica decide ese destinatario.
        var categories = many.InnerExceptions.Select(e => ClassifyStatus((int)e.StatusCode)).ToList();
        if (categories.Count == 0)
            return ClassifyStatus((int)many.StatusCode);
        return categories.Contains(CommunicationFailureCategory.Transient)
            ? CommunicationFailureCategory.Transient
            : categories[0];
    }

    private static CommunicationFailureCategory ClassifySmtp(SmtpException smtp)
    {
        // GeneralFailure: no hubo respuesta SMTP (conexión/DNS/TLS) — decide la causa interna.
        if (smtp.StatusCode == SmtpStatusCode.GeneralFailure)
        {
            return FindInner<SocketException>(smtp) is { } socket
                ? ClassifySocket(socket)
                : FindInner<IOException>(smtp) is not null
                    ? CommunicationFailureCategory.Transient
                    : CommunicationFailureCategory.Unknown;
        }

        return ClassifyStatus((int)smtp.StatusCode);
    }

    private static CommunicationFailureCategory ClassifyStatus(int code)
    {
        if (ConfigurationCodes.Contains(code))
            return CommunicationFailureCategory.Configuration;
        if (code is >= 400 and < 500)
            return CommunicationFailureCategory.Transient;
        if (PermanentCodes.Contains(code))
            return CommunicationFailureCategory.Permanent;
        return CommunicationFailureCategory.Unknown;
    }

    private static CommunicationFailureCategory ClassifySocket(SocketException socket) =>
        socket.SocketErrorCode switch
        {
            // El host configurado no existe/no resuelve: no se arregla reintentando.
            SocketError.HostNotFound or SocketError.NoData => CommunicationFailureCategory.Configuration,
            _ => CommunicationFailureCategory.Transient,
        };

    private static T? FindInner<T>(Exception exception)
        where T : Exception
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is T match)
                return match;
        }

        return null;
    }
}
