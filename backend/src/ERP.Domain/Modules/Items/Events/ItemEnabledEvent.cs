using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemEnabledEvent : BaseDomainEvent
{
    public Guid ItemId { get; }

    public ItemEnabledEvent(Guid itemId, Guid subscriberId)
    {
        ItemId       = itemId;
        SubscriberId = subscriberId;
    }
}
