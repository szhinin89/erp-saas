using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Events;

/// <summary>
/// Se levanta cuando el PVP snapshot de una línea de compra cambia — tanto por edición manual
/// en borrador (<c>PurchaseInvoice.UpdateLinePvp()</c>, action "Updated") como por la escritura
/// automática de <c>Item.BaseSalePrice</c> al confirmar (<c>PurchaseInvoice.RecordConfirmedItemPvpUpdate()</c>,
/// action "ConfirmedUpdate"). <see cref="ItemId"/> identifica el ítem afectado — consistente en
/// ambos casos aunque solo el segundo mute el precio base global del ítem.
/// </summary>
public sealed class PurchaseLinePvpUpdatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public Guid ItemId { get; }
    public decimal OldPvp { get; }
    public decimal NewPvp { get; }
    public string ActionName { get; }

    public PurchaseLinePvpUpdatedEvent(
        Guid tenantId,
        Guid invoiceId,
        string invoiceNumber,
        Guid itemId,
        decimal oldPvp,
        decimal newPvp,
        string actionName
    )
    {
        TenantId = tenantId;
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        ItemId = itemId;
        OldPvp = oldPvp;
        NewPvp = newPvp;
        ActionName = actionName;
    }

    Guid IAuditEvent.EntityId => ItemId;
    string IAuditEvent.Action => ActionName;
    string? IAuditEvent.Reason => null;
}
