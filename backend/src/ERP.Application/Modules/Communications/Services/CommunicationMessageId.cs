namespace ERP.Application.Modules.Communications.Services;

/// <summary>
/// Message-ID de una comunicación: depende ÚNICAMENTE de <c>CommunicationOutbox.Id</c> (inmutable),
/// nunca de la configuración SMTP (remitente, dominio, host), que puede cambiar entre reintentos.
/// El lado derecho es una constante con forma de dominio (RFC 5322 §3.6.4, <c>id-right</c>).
/// <para>
/// Es una mitigación y un dato de trazabilidad: el mismo correo reenviado tras una recuperación
/// lleva el mismo Message-ID y los clientes pueden deduplicarlo. No se asume que el servidor SMTP
/// deduplique.
/// </para>
/// </summary>
public static class CommunicationMessageId
{
    public const string Domain = "communications.zh-erp";

    public static string For(Guid communicationId) => $"<{communicationId:N}@{Domain}>";
}
