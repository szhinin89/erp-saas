using ERP.Domain.Common;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemCreatedEvent : BaseDomainEvent
{
    public Guid ItemId { get; }
    public string SKU { get; }
    public ItemType ItemType { get; }

    public ItemCreatedEvent(Guid itemId, string sku, ItemType itemType, Guid subscriberId)
    {
        ItemId      = itemId;
        SKU         = sku;
        ItemType    = itemType;
        SubscriberId = subscriberId;
    }
}
