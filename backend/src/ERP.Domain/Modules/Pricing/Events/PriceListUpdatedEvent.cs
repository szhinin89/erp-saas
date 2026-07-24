using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Events;

/// <summary>
/// Se levanta cuando <c>PriceList.Update()</c> cambia la regla general u otros datos de la
/// lista. Consumido por <c>PriceListAuditHandler</c>, que lo traduce a <c>PriceListAudit</c>
/// — mismo patrón que PricingRule/PriceListItem.
/// </summary>
public sealed class PriceListUpdatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PriceListId { get; }
    public PricingRuleType? OldRuleType { get; }
    public decimal? OldRuleValue { get; }
    public PricingRuleType? NewRuleType { get; }
    public decimal? NewRuleValue { get; }

    public PriceListUpdatedEvent(
        Guid tenantId, Guid priceListId,
        PricingRuleType? oldRuleType, decimal? oldRuleValue,
        PricingRuleType? newRuleType, decimal? newRuleValue)
    {
        TenantId = tenantId;
        PriceListId = priceListId;
        OldRuleType = oldRuleType;
        OldRuleValue = oldRuleValue;
        NewRuleType = newRuleType;
        NewRuleValue = newRuleValue;
    }

    Guid IAuditEvent.EntityId => PriceListId;
    string IAuditEvent.Action => "Updated";
    string? IAuditEvent.Reason => null;
}
