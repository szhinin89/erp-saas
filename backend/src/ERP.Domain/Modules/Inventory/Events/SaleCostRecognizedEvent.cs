using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Events;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — el Kardex cambió el costo reconocido de una venta (cobertura de costo
/// pendiente o devolución de venta valorizada). <see cref="Amount"/> es el delta monetario con signo
/// (positivo = más costo de ventas). Es el hecho económico; Accounting decide si genera obligación.
/// </summary>
public sealed class SaleCostRecognizedEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid InvoiceId { get; }
    public Guid SourceEventId { get; }
    public decimal Amount { get; }
    public DateOnly EntryDate { get; }
    public string Kind { get; }

    /// <summary>
    /// Cobertura/devolución: costo de ventas objetivo de la factura tras el cambio (redondeado). El delta contable
    /// se resuelve al contabilizar contra lo ya contabilizado; <see cref="Amount"/> es solo el delta operativo.
    /// </summary>
    public decimal? RecognizedCostTarget { get; }

    public SaleCostRecognizedEvent(Guid tenantId, Guid companyId, Guid invoiceId, Guid sourceEventId,
        decimal amount, DateOnly entryDate, string kind, decimal? recognizedCostTarget = null)
    {
        RecognizedCostTarget = recognizedCostTarget;
        TenantId = tenantId; CompanyId = companyId; InvoiceId = invoiceId; SourceEventId = sourceEventId;
        Amount = amount; EntryDate = entryDate; Kind = kind;
    }
}
