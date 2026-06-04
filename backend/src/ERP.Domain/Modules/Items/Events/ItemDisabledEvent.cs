using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemDisabledEvent : BaseDomainEvent
{
    public Guid ItemId { get; }

    public ItemDisabledEvent(Guid itemId, Guid subscriberId)
    {
        ItemId       = itemId;
        SubscriberId = subscriberId;
    }
}
