using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemCreatedEvent : BaseDomainEvent, IIntegrationEvent, IAuditEvent
{
    public Guid ItemId { get; }
    public string SKU { get; }
    public Guid ItemTypeId { get; }

    public ItemCreatedEvent(Guid itemId, string sku, Guid itemTypeId, Guid tenantId)
    {
        ItemId = itemId;
        SKU = sku;
        ItemTypeId = itemTypeId;
        TenantId = tenantId;
    }

    Guid IAuditEvent.EntityId => ItemId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}
