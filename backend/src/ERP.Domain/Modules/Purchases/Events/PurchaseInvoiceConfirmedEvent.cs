using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>Se levanta cuando <c>PurchaseInvoice.Confirm()</c> pasa la compra de Draft a Confirmed.</summary>
public sealed class PurchaseInvoiceConfirmedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid InvoiceId { get; }
    public Guid SupplierId { get; }
    public string InvoiceNumber { get; }
    public decimal GrandTotal { get; }

    public PurchaseInvoiceConfirmedEvent(
        Guid tenantId, Guid invoiceId, Guid supplierId, string invoiceNumber, decimal grandTotal)
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber;
        GrandTotal = grandTotal;
    }

    Guid IAuditEvent.EntityId => InvoiceId;
    string IAuditEvent.Action => "Confirmed";
    string? IAuditEvent.Reason => null;
}
