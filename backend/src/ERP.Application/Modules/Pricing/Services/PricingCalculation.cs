using ERP.Application.Modules.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Application.Modules.Pricing.Services;

/// <summary>
/// Núcleo puro de la precedencia de reglas del motor de Pricing (Regla del ítem &gt; Regla
/// general de la lista &gt; sin ajuste). Extraído de <see cref="PricingResolver"/> para que
/// cualquier consumidor que necesite resolver precio contra una o varias <see cref="PriceList"/>
/// (resolución puntual o simulación batch) use exactamente el mismo cálculo — cero
/// reimplementación de la fórmula en ningún otro punto del sistema.
/// </summary>
public static class PricingCalculation
{
    /// <summary>Única fuente de verdad de la precedencia — Resolve y Summarize comparten esta clasificación.</summary>
    public static PriceSource ClassifySource(PricingRule? itemRule, PriceList priceList) =>
        itemRule is not null ? PriceSource.Exception
        : priceList.RuleType.HasValue ? PriceSource.GeneralRule
        : PriceSource.BasePrice;

    public static (decimal UnitPrice, string? RuleApplied) Resolve(
        decimal basePrice,
        PricingRule? itemRule,
        PriceList priceList,
        IPricingAdjustmentStrategyResolver strategies)
    {
        decimal unitPrice;
        string? ruleApplied;

        switch (ClassifySource(itemRule, priceList))
        {
            case PriceSource.Exception:
                unitPrice = strategies.Resolve(itemRule!.RuleType).Apply(basePrice, itemRule.RuleValue);
                ruleApplied = $"{itemRule.RuleType}:{itemRule.RuleValue:0.######} (ítem)";
                break;
            case PriceSource.GeneralRule:
                unitPrice = strategies.Resolve(priceList.RuleType!.Value).Apply(basePrice, priceList.RuleValue!.Value);
                ruleApplied = $"{priceList.RuleType}:{priceList.RuleValue:0.######} (lista)";
                break;
            default:
                unitPrice = basePrice;
                ruleApplied = null;
                break;
        }

        unitPrice = Math.Round(unitPrice, 6, MidpointRounding.AwayFromZero);
        if (unitPrice < 0) unitPrice = 0;

        return (unitPrice, ruleApplied);
    }

    /// <summary>
    /// Resumen encapsulado (Source/Type/Value/Description) del mismo cálculo de precedencia que
    /// <see cref="Resolve"/> — usado por read-models que no deben exponer RuleType/RuleValue
    /// sueltos (ver <see cref="PricingRuleSummaryDto"/>).
    /// </summary>
    public static PricingRuleSummaryDto Summarize(PricingRule? itemRule, PriceList priceList) =>
        ClassifySource(itemRule, priceList) switch
        {
            PriceSource.Exception => new PricingRuleSummaryDto(
                PriceSource.Exception, itemRule!.RuleType.ToString(), itemRule.RuleValue,
                $"{DescribeRule(itemRule.RuleType, itemRule.RuleValue)} (excepción)"),
            PriceSource.GeneralRule => new PricingRuleSummaryDto(
                PriceSource.GeneralRule, priceList.RuleType!.Value.ToString(), priceList.RuleValue,
                $"{DescribeRule(priceList.RuleType.Value, priceList.RuleValue!.Value)} (regla general)"),
            _ => new PricingRuleSummaryDto(PriceSource.BasePrice, null, null, "Precio base"),
        };

    private static string DescribeRule(PricingRuleType type, decimal value) => type switch
    {
        PricingRuleType.PercentDiscount => $"Descuento {value:0.##}%",
        PricingRuleType.PercentMarkup   => $"Recargo {value:0.##}%",
        PricingRuleType.FixedPrice      => $"Precio fijo {value:0.##}",
        PricingRuleType.FixedAdjustment => $"Ajuste {(value >= 0 ? "+" : "")}{value:0.##}",
        _ => type.ToString(),
    };
}
