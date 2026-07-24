using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Pricing;

public sealed class PricingRuleTests
{
    private static PricingRule CreateRule(PricingRuleType type = PricingRuleType.FixedPrice, decimal value = 10m) =>
        PricingRule.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, value, Guid.NewGuid());

    [Fact]
    public void Create_es_activa_por_defecto()
    {
        var rule = CreateRule();
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_con_FixedPrice_negativo_lanza_ArgumentException()
    {
        var act = () => CreateRule(PricingRuleType.FixedPrice, -1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_PercentDiscount_negativo_es_valido_porque_no_es_FixedPrice()
    {
        var act = () => CreateRule(PricingRuleType.PercentDiscount, -5m);
        act.Should().NotThrow();
    }

    [Fact]
    public void Disable_desactiva_la_regla_sin_eliminarla()
    {
        var rule = CreateRule();
        rule.Disable(Guid.NewGuid());
        rule.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Disable_dos_veces_lanza_InvalidOperationException()
    {
        var rule = CreateRule();
        rule.Disable(Guid.NewGuid());
        var act = () => rule.Disable(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Enable_reactiva_una_regla_deshabilitada()
    {
        var rule = CreateRule();
        rule.Disable(Guid.NewGuid());
        rule.Enable(Guid.NewGuid());
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Enable_sobre_regla_activa_lanza_InvalidOperationException()
    {
        var rule = CreateRule();
        var act = () => rule.Enable(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateRule_sobrescribe_el_valor_sin_conservar_el_anterior_en_el_dominio()
    {
        var rule = CreateRule(PricingRuleType.FixedPrice, 10m);
        rule.UpdateRule(PricingRuleType.PercentDiscount, 15m, Guid.NewGuid());
        rule.RuleType.Should().Be(PricingRuleType.PercentDiscount);
        rule.RuleValue.Should().Be(15m);
    }
}
