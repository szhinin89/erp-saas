using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Services;

/// <summary>
/// Transforma el precio base de un ítem según un tipo de regla. Pura, sin I/O.
/// Cada estrategia se registra en DI (Application) y se resuelve por RuleType —
/// agregar un tipo de regla nuevo no requiere tocar PricingResolver (OCP).
/// </summary>
public interface IPricingAdjustmentStrategy
{
    PricingRuleType RuleType { get; }
    decimal Apply(decimal basePrice, decimal ruleValue);
}
