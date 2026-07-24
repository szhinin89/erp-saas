namespace ERP.Domain.Modules.Pricing.Enums;

/// <summary>
/// Estrategia de transformación de una PricingRule sobre el precio base del ítem.
/// Abierto a extensión: nuevos tipos requieren una nueva IPricingAdjustmentStrategy,
/// nunca modificar PricingResolver ni las entidades existentes (OCP).
/// </summary>
public enum PricingRuleType
{
    FixedPrice       = 1,
    PercentDiscount  = 2,
    PercentMarkup    = 3,
    FixedAdjustment  = 4,
}
