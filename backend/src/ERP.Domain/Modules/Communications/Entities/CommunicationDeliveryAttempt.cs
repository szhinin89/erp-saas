using ERP.Domain.Common;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Entities;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D11) — historial de un intento de entrega. La
/// <see cref="CommunicationOutbox"/> es el estado ACTUAL; esto es HISTORIAL y nunca se lee para
/// decidirlo. Lo escribe solo <c>CommunicationOutboxDeliveryStore</c> (SQL condicionado por
/// <see cref="ClaimToken"/>): se abre al reclamar y se cierra al finalizar.
/// <para>
/// Nunca contiene cuerpos, destinatarios, adjuntos, tokens, contraseñas ni credenciales:
/// <see cref="ErrorSafeText"/> es un resumen sanitizado (tipo de error + código del proveedor).
/// </para>
/// </summary>
public sealed class CommunicationDeliveryAttempt : SystemBaseEntity, IOptionalCompanyScopeEntity
{
    public const int TransportMaxLen = 30;
    public const int ProviderCodeMaxLen = 50;
    public const int ProviderMessageIdMaxLen = 300;
    public const int ErrorSafeTextMaxLen = 1000;

    public Guid CommunicationId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid ClaimToken { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string Transport { get; private set; } = null!;

    /// <summary>Null mientras el intento está en curso.</summary>
    public CommunicationAttemptResult? Result { get; private set; }
    public CommunicationFailureCategory? FailureCategory { get; private set; }
    public string? ProviderCode { get; private set; }

    /// <summary>Id real devuelto por el proveedor si existe (System.Net.Mail no lo expone: null). ≠ Message-ID propio.</summary>
    public string? ProviderMessageId { get; private set; }
    public string? ErrorSafeText { get; private set; }

    private CommunicationDeliveryAttempt() { }
}
