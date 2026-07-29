using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemEnabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ItemId { get; }

    public ItemEnabledEvent(Guid itemId, Guid tenantId)
    {
        ItemId = itemId;
        TenantId = tenantId;
    }

    Guid IAuditEvent.EntityId => ItemId;
    string IAuditEvent.Action => "Enabled";
    string? IAuditEvent.Reason => null;
}
