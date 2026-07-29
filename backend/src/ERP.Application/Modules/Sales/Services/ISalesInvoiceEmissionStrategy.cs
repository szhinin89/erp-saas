using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Sales.Services;

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
    /// Ejecuta el flujo posterior a la autorización correspondiente a <see cref="SupportedType"/>.
    /// </summary>
    /// <returns>
    /// Mensaje de error informativo si algo falló (nunca lanza — la autorización comercial ya
    /// está confirmada e irreversible en este punto); <c>null</c> si no hubo error.
    /// </returns>
    Task<string?> ExecuteAsync(SalesInvoiceEmissionContext context, CancellationToken ct);
}
