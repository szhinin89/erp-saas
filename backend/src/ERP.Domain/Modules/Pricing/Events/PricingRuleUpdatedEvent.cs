using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Events;

/// <summary>Se levanta cuando <c>PricingRule.UpdateRule()</c> cambia el valor de una regla ya activa.</summary>
public sealed class PricingRuleUpdatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RuleId { get; }
    public Guid PriceListId { get; }
    public Guid ItemId { get; }
    public PricingRuleType OldRuleType { get; }
    public decimal OldRuleValue { get; }
    public PricingRuleType NewRuleType { get; }
    public decimal NewRuleValue { get; }

    public PricingRuleUpdatedEvent(
        Guid tenantId, Guid ruleId, Guid priceListId, Guid itemId,
        PricingRuleType oldRuleType, decimal oldRuleValue,
        PricingRuleType newRuleType, decimal newRuleValue)
    {
        TenantId = tenantId;
        RuleId = ruleId;
        PriceListId = priceListId;
        ItemId = itemId;
        OldRuleType = oldRuleType;
        OldRuleValue = oldRuleValue;
        NewRuleType = newRuleType;
        NewRuleValue = newRuleValue;
    }

    Guid IAuditEvent.EntityId => RuleId;
    string IAuditEvent.Action => "Updated";
    string? IAuditEvent.Reason => null;
}
