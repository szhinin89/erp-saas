using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Events;

/// <summary>Se levanta cuando <c>Item.UpdateBaseSalePrice()</c> cambia realmente el precio base (SSOT del Motor de Pricing v2).</summary>
public sealed class ItemPriceChangedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ItemId { get; }
    public decimal? OldBaseSalePrice { get; }
    public decimal? NewBaseSalePrice { get; }

    public ItemPriceChangedEvent(
        Guid itemId,
        Guid tenantId,
        decimal? oldBaseSalePrice,
        decimal? newBaseSalePrice
    )
    {
        ItemId = itemId;
        TenantId = tenantId;
        OldBaseSalePrice = oldBaseSalePrice;
        NewBaseSalePrice = newBaseSalePrice;
    }

    Guid IAuditEvent.EntityId => ItemId;
    string IAuditEvent.Action => "PriceChanged";
    string? IAuditEvent.Reason => null;
}
