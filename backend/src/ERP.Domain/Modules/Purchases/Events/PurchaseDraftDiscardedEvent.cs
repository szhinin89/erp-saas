using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// FASE 5 PURCHASE-UX — se levanta cuando <c>PurchaseInvoice.DiscardDraft()</c> descarta un borrador.
/// Solo para auditoría: a diferencia de <see cref="PurchaseInvoiceCancelledEvent"/>, ningún
/// traductor de posting, CxP, inventario ni retención lo consume (un borrador no generó nada que
/// revertir).
/// </summary>
public sealed class PurchaseDraftDiscardedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid InvoiceId { get; }
    public Guid SupplierId { get; }
    public string InvoiceNumber { get; }
    public string Reason { get; }

    public PurchaseDraftDiscardedEvent(
        Guid tenantId,
        Guid invoiceId,
        Guid supplierId,
        string invoiceNumber,
        string reason
    )
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber;
        Reason = reason;
    }

    Guid IAuditEvent.EntityId => InvoiceId;
    string IAuditEvent.Action => "DraftDiscarded";
    string? IAuditEvent.Reason => Reason;
}
