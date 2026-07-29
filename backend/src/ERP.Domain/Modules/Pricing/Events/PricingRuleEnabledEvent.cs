using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Events;

/// <summary>Se levanta cuando <c>PricingRule.Enable()</c> reactiva una regla deshabilitada.</summary>
public sealed class PricingRuleEnabledEvent : BaseDomainEvent, IAuditEvent
{
    public Guid RuleId { get; }
    public Guid PriceListId { get; }
    public Guid ItemId { get; }
    public PricingRuleType RuleType { get; }
    public decimal RuleValue { get; }

    public PricingRuleEnabledEvent(
        Guid tenantId,
        Guid ruleId,
        Guid priceListId,
        Guid itemId,
        PricingRuleType ruleType,
        decimal ruleValue
    )
    {
        TenantId = tenantId;
        RuleId = ruleId;
        PriceListId = priceListId;
        ItemId = itemId;
        RuleType = ruleType;
        RuleValue = ruleValue;
    }

    Guid IAuditEvent.EntityId => RuleId;
    string IAuditEvent.Action => "Enabled";
    string? IAuditEvent.Reason => null;
}
