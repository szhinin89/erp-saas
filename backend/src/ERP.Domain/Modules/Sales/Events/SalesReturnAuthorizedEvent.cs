using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

/// <summary>
/// Devolución de venta autorizada (P0-01). Implementa <see cref="IAuditEvent"/> — listo para que
/// una fase posterior lo audite (Entity Audit) sin modificar este evento. Los 5 campos de monto
/// replican exactamente el contrato exigido por <c>PostingFact</c> (Accounting), consumidos tal
/// cual por el translator de una fase posterior, nunca recalculados.
/// </summary>
public sealed class SalesReturnAuthorizedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid SalesReturnId { get; }
    public Guid SalesInvoiceId { get; }
    public string ReturnNumber { get; }
    public decimal GrandTotal { get; }
    public Guid UserId { get; }
    public Guid CompanyId { get; }

    /// <summary>
    /// Montos ya resueltos por el dominio de <c>SalesReturn</c> — Accounting los consume tal cual,
    /// nunca los recalcula (mismo criterio que <c>SalesInvoiceAuthorizedEvent</c>).
    /// </summary>
    public decimal Subtotal { get; }
    public decimal TotalVat { get; }
    public decimal TotalIce { get; }
    public decimal TotalDiscount { get; }

    public string Reason { get; }

    Guid IAuditEvent.EntityId => SalesReturnId;
    string IAuditEvent.Action => "Authorized";
    string? IAuditEvent.Reason => Reason;

    public SalesReturnAuthorizedEvent(
        Guid salesReturnId,
        Guid salesInvoiceId,
        string returnNumber,
        decimal grandTotal,
        Guid userId,
        Guid tenantId,
        Guid companyId,
        decimal subtotal,
        decimal totalVat,
        decimal totalIce,
        decimal totalDiscount,
        string reason
    )
    {
        SalesReturnId = salesReturnId;
        SalesInvoiceId = salesInvoiceId;
        ReturnNumber = returnNumber;
        GrandTotal = grandTotal;
        UserId = userId;
        TenantId = tenantId;
        CompanyId = companyId;
        Subtotal = subtotal;
        TotalVat = totalVat;
        TotalIce = totalIce;
        TotalDiscount = totalDiscount;
        Reason = reason;
    }
}
