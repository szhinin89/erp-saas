using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Único punto de contacto de Ride con ElectronicDocuments (ADR-025 §8) — consume exclusivamente
/// los requests públicos de lectura de ese módulo (nunca su entidad ni sus repositorios), y
/// traduce su vocabulario (<c>ElectronicDocumentType</c>) a <see cref="RideDocumentType"/>.
/// Identifica el documento por <c>(sourceModule, sourceEntityId)</c> — el mismo dato que ya
/// tiene el módulo consumidor (ADR-025 §7, corrección H2) — nunca por el Id interno de
/// <c>ElectronicDocument</c>.
/// </summary>
public interface IRideSourceXmlProvider
{
    /// <summary>
    /// <see cref="Result{T}.Failure"/> queda reservado a fallos de infraestructura reales — los
    /// tres desenlaces de negocio conocidos (disponible / todavía no autorizado con XML pendiente
    /// / nunca tendrá RIDE) viajan dentro de <see cref="Result{T}.Success"/>, discriminados por
    /// <see cref="RideSourceXmlLookup.Status"/> (H1, ADR-025 §7).
    /// </summary>
    Task<Result<RideSourceXmlLookup>> GetAuthorizedXmlAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId, CancellationToken ct = default);
}

/// <summary>
/// Tipo interno de retorno de <see cref="IRideSourceXmlProvider"/> — no es uno de los DTOs
/// públicos de ADR-025 §7, es el payload de un contrato interno (mismo estatus que
/// <c>RideTemplateSelector</c>): necesario para que <see cref="RideSourceXmlStatus.PendingSource"/>
/// (H1) sea distinguible de <see cref="RideSourceXmlStatus.NotApplicable"/> sin modificar el
/// tipo compartido <see cref="Result{T}"/>.
/// </summary>
public sealed record RideSourceXmlLookup(
    RideSourceXmlStatus Status,
    string? AuthorizedXml,
    Guid ElectronicDocumentId,
    RideDocumentType DocumentType);

public enum RideSourceXmlStatus
{
    /// <summary>El XML autorizado está disponible y listo para parsear.</summary>
    Available,

    /// <summary>El documento está autorizado en el SRI pero el XML todavía no está persistido (H1) — reintentable.</summary>
    PendingSource,

    /// <summary>El documento nunca tendrá RIDE (rechazado, cancelado, o no existe) — no reintentable.</summary>
    NotApplicable,
}
