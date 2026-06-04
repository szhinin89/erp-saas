using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

public sealed class ItemVariantAddedEvent : BaseDomainEvent
{
    public Guid ItemId { get; }
    public Guid VariantId { get; }
    public string VariantSKU { get; }

    public ItemVariantAddedEvent(Guid itemId, Guid variantId, string variantSku, Guid subscriberId)
    {
        ItemId       = itemId;
        VariantId    = variantId;
        VariantSKU   = variantSku;
        SubscriberId = subscriberId;
    }
}
