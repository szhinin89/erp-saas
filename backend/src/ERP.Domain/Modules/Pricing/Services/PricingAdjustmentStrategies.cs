using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Services;

public sealed class FixedPriceStrategy : IPricingAdjustmentStrategy
{
    public PricingRuleType RuleType => PricingRuleType.FixedPrice;
    public decimal Apply(decimal basePrice, decimal ruleValue) => ruleValue;
}

public sealed class PercentDiscountStrategy : IPricingAdjustmentStrategy
{
    public PricingRuleType RuleType => PricingRuleType.PercentDiscount;
    public decimal Apply(decimal basePrice, decimal ruleValue) => basePrice * (1 - ruleValue / 100m);
}

public sealed class PercentMarkupStrategy : IPricingAdjustmentStrategy
{
    public PricingRuleType RuleType => PricingRuleType.PercentMarkup;
    public decimal Apply(decimal basePrice, decimal ruleValue) => basePrice * (1 + ruleValue / 100m);
}

public sealed class FixedAdjustmentStrategy : IPricingAdjustmentStrategy
{
    public PricingRuleType RuleType => PricingRuleType.FixedAdjustment;
    public decimal Apply(decimal basePrice, decimal ruleValue) => basePrice + ruleValue;
}
