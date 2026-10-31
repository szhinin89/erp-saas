using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// R12 Fase A (ADR-036, I-1) — origen "Sales": la Factura 01 (<c>SourceEntityId</c> = factura) y la
/// Nota de Crédito 04 (<c>SourceEntityId</c> = devolución) comparten módulo de origen. Solo una factura
/// o devolución <c>Authorized</c> permite procesar su comprobante; Draft, Cancelled o inexistente en el
/// alcance tenant + empresa no (fail-closed). El lock es la misma fila que toma la anulación de la
/// factura (<c>GetByIdForUpdateAsync</c>), así que reclamar el envío y anular se serializan.
///
/// <see cref="UsesDispatchProtocol"/> es false: el pipeline v1.0 de Ventas (Signed, job de reintentos,
/// consulta por clave de acceso) no cambia; solo se agrega la revalidación del origen bajo lock.
/// </summary>
public sealed class SalesElectronicSourceLifecycleGuard : IElectronicDocumentSourceLifecycleGuard
{
    public const string SalesSourceModule = "Sales";

    private readonly ISalesInvoiceRepository _invoices;
    private readonly ISalesReturnRepository _returns;

    public SalesElectronicSourceLifecycleGuard(
        ISalesInvoiceRepository invoices,
        ISalesReturnRepository returns
    )
    {
        _invoices = invoices;
        _returns = returns;
    }

    public string SourceModule => SalesSourceModule;

    public bool UsesDispatchProtocol => false;

    public async Task<ElectronicDocumentSourceLifecycle> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        Guid sourceEntityId,
        bool lockForUpdate,
        CancellationToken ct = default
    )
    {
        var invoiceStatus = await _invoices.GetCurrentStatusAsync(
            tenantId,
            companyId,
            sourceEntityId,
            lockForUpdate,
            ct
        );
        if (invoiceStatus is not null)
            return invoiceStatus switch
            {
                SalesInvoiceStatus.Authorized => ElectronicDocumentSourceLifecycle.Allowed,
                SalesInvoiceStatus.Cancelled => ElectronicDocumentSourceLifecycle.Denied(
                    "La factura está anulada: su comprobante electrónico no puede procesarse."
                ),
                _ => ElectronicDocumentSourceLifecycle.Denied(
                    $"La factura debe estar autorizada para procesar su comprobante electrónico (estado actual: {invoiceStatus})."
                ),
            };

        var returnStatus = await _returns.GetCurrentStatusAsync(
            tenantId,
            companyId,
            sourceEntityId,
            lockForUpdate,
            ct
        );
        return returnStatus switch
        {
            SalesReturnStatus.Authorized => ElectronicDocumentSourceLifecycle.Allowed,
            null => ElectronicDocumentSourceLifecycle.Denied(
                "El documento de origen de ventas no existe."
            ),
            _ => ElectronicDocumentSourceLifecycle.Denied(
                $"La devolución debe estar autorizada para procesar su Nota de Crédito electrónica (estado actual: {returnStatus})."
            ),
        };
    }
}
