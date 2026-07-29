using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Services;

namespace ERP.Application.Modules.Pricing.Services;

/// <summary>
/// Resuelve la IPricingAdjustmentStrategy registrada en DI para un RuleType. Agregar una
/// estrategia nueva solo requiere registrarla en DependencyInjection — nunca tocar PricingResolver.
/// </summary>
public interface IPricingAdjustmentStrategyResolver
{
    IPricingAdjustmentStrategy Resolve(PricingRuleType ruleType);
}

public sealed class PricingAdjustmentStrategyResolver : IPricingAdjustmentStrategyResolver
{
    private readonly IReadOnlyDictionary<PricingRuleType, IPricingAdjustmentStrategy> _strategies;

    public PricingAdjustmentStrategyResolver(IEnumerable<IPricingAdjustmentStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.RuleType);
    }

    public IPricingAdjustmentStrategy Resolve(PricingRuleType ruleType) =>
        _strategies.TryGetValue(ruleType, out var strategy)
            ? strategy
            : throw new InvalidOperationException(
                $"No hay una estrategia de ajuste registrada para el tipo de regla '{ruleType}'."
            );
}
