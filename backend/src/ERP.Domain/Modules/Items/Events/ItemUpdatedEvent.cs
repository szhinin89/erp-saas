using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

/// <summary>
/// Se levanta una vez por operación de edición completa del ítem, desde
/// <c>Item.UpdateIdentity()</c> — el único método de actualización que
/// <c>UpdateItemCommandHandler</c> invoca siempre exactamente una vez por request, junto a
/// los demás <c>Update*</c> (clasificación, UOM, impuestos, venta, stock, precio). Levantar
/// el evento desde cada <c>Update*</c> individual produciría varias filas de auditoría para
/// una sola acción de usuario ("Guardar"); este evento representa esa acción como una unidad.
/// El cambio de precio base tiene su propio evento dedicado (<see cref="ItemPriceChangedEvent"/>)
/// porque sí tiene un valor anterior/nuevo típico de auditar.
/// </summary>
public sealed class ItemUpdatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ItemId { get; }

    public ItemUpdatedEvent(Guid itemId, Guid tenantId)
    {
        ItemId = itemId;
        TenantId = tenantId;
    }

    Guid IAuditEvent.EntityId => ItemId;
    string IAuditEvent.Action => "Updated";
    string? IAuditEvent.Reason => null;
}
