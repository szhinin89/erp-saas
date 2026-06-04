using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemVariantDisabledEvent : BaseDomainEvent
{
    public Guid ItemId { get; }
    public Guid VariantId { get; }

    public ItemVariantDisabledEvent(Guid itemId, Guid variantId, Guid subscriberId)
    {
        ItemId       = itemId;
        VariantId    = variantId;
        SubscriberId = subscriberId;
    }
}
