using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Pricing;

public sealed class PriceListRuleTests
{
    private static PriceList CreateList(
        PricingRuleType? ruleType = null,
        decimal? ruleValue = null
    ) =>
        PriceList.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MAYORISTA",
            "Mayorista",
            "USD",
            isDefault: false,
            createdBy: Guid.NewGuid(),
            ruleType: ruleType,
            ruleValue: ruleValue
        );

    [Fact]
    public void Create_sin_regla_general_es_valido()
    {
        var act = () => CreateList();
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_con_RuleType_sin_RuleValue_lanza_ArgumentException()
    {
        var act = () => CreateList(PricingRuleType.PercentDiscount, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_RuleValue_sin_RuleType_lanza_ArgumentException()
    {
        var act = () =>
            PriceList.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "MAYORISTA",
                "Mayorista",
                "USD",
                false,
                Guid.NewGuid(),
                ruleType: null,
                ruleValue: 10m
            );
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsValidOn_sin_fechas_de_vigencia_siempre_es_verdadero()
    {
        var list = CreateList();
        list.IsValidOn(DateOnly.FromDateTime(DateTime.UtcNow)).Should().BeTrue();
    }

    [Fact]
    public void IsValidOn_fuera_del_rango_de_vigencia_es_falso()
    {
        var list = PriceList.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "PROMO",
            "Promo",
            "USD",
            false,
            Guid.NewGuid(),
            validFrom: new DateOnly(2020, 1, 1),
            validUntil: new DateOnly(2020, 1, 31)
        );
        list.IsValidOn(new DateOnly(2020, 2, 1)).Should().BeFalse();
    }
}
