using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>Se levanta cuando <c>PurchaseInvoice.Cancel()</c> anula una compra confirmada.</summary>
public sealed class PurchaseInvoiceCancelledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid InvoiceId { get; }
    public Guid SupplierId { get; }
    public string InvoiceNumber { get; }
    public decimal GrandTotal { get; }
    public string CancelReason { get; }

    public PurchaseInvoiceCancelledEvent(
        Guid tenantId,
        Guid invoiceId,
        Guid supplierId,
        string invoiceNumber,
        decimal grandTotal,
        string cancelReason
    )
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
        CancelReason = cancelReason;
    }

    Guid IAuditEvent.EntityId => InvoiceId;
    string IAuditEvent.Action => "Cancelled";
    string? IAuditEvent.Reason => CancelReason;
}
