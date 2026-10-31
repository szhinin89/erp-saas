using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// READINESS-PREVENTIVE-1B — datos para comprobar requisitos de emisión ANTES de capturar el
/// secuencial y de cualquier efecto comercial (la factura sigue en Draft).
/// </summary>
public sealed record SalesInvoiceEmissionPreflight(
    Guid TenantId,
    Guid CompanyId,
    Guid EmissionPointId,
    string DocTypeCode
);

/// <summary>
/// Datos que necesita cualquier estrategia de emisión para actuar sobre una factura ya
/// autorizada comercialmente (<see cref="SalesInvoice.Authorize"/>) — nunca antes de eso.
/// </summary>
public sealed record SalesInvoiceEmissionContext(
    Guid TenantId,
    Guid CompanyId,
    Guid UserId,
    SalesInvoice Invoice
);

/// <summary>
/// Único punto de decisión de "qué pasa después de autorizar una factura, según el tipo de
/// emisión de su Punto de Emisión" (Electronic vs Physical). <see cref="AuthorizeSalesInvoiceHandler"/>
/// solo resuelve el <see cref="EmissionType"/> del Punto de Emisión y delega aquí — nunca vuelve a
/// distribuir un if/else de ElectronicDocuments dentro de un handler.
/// </summary>
public interface ISalesInvoiceEmissionStrategy
{
    EmissionType SupportedType { get; }

    /// <summary>
    /// Requisitos locales que <see cref="SupportedType"/> exige para poder iniciar la emisión. Se
    /// invoca antes de capturar secuencial: nunca contacta al SRI ni produce efectos.
    /// </summary>
    Task<ElectronicEmissionRequirementsResult> CheckRequirementsAsync(
        SalesInvoiceEmissionPreflight preflight,
        CancellationToken ct
    );

    /// <summary>
    /// PHYSICAL-FISCAL-F1 — rangos de secuencial que esta modalidad autoriza para el punto y tipo;
    /// <c>null</c> = sin límite. Se pasan tal cual a
    /// <see cref="IDocumentSequenceRepository.CaptureNextAsync(Guid, Guid, Guid, string, IReadOnlyList{AuthorizedSequentialRange}, CancellationToken)"/>,
    /// que los comprueba bajo el lock de la secuencia.
    /// </summary>
    Task<IReadOnlyList<AuthorizedSequentialRange>?> ResolveAuthorizedSequentialRangesAsync(
        SalesInvoiceEmissionPreflight preflight,
        CancellationToken ct
    );

    /// <summary>
    /// Ejecuta el flujo posterior a la autorización correspondiente a <see cref="SupportedType"/>.
    /// </summary>
    /// <returns>
    /// Mensaje de error informativo si algo falló (nunca lanza — la autorización comercial ya
    /// está confirmada e irreversible en este punto); <c>null</c> si no hubo error.
    /// </returns>
    Task<string?> ExecuteAsync(SalesInvoiceEmissionContext context, CancellationToken ct);
}
