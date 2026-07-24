using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemDisabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ItemId { get; }

    public ItemDisabledEvent(Guid itemId, Guid tenantId)
    {
        ItemId       = itemId;
        TenantId = tenantId;
    }

    Guid IAuditEvent.EntityId => ItemId;
    string IAuditEvent.Action => "Disabled";
    string? IAuditEvent.Reason => null;
}
