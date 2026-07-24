using ERP.Domain.Modules.Pricing.Services;
using FluentAssertions;

namespace ERP.Domain.Tests.Pricing;

public sealed class PricingAdjustmentStrategyTests
{
    [Fact]
    public void FixedPriceStrategy_ignora_el_precio_base_y_devuelve_el_valor_de_la_regla()
        => new FixedPriceStrategy().Apply(basePrice: 100m, ruleValue: 55m).Should().Be(55m);

    [Fact]
    public void PercentDiscountStrategy_resta_el_porcentaje_sobre_el_precio_base()
        => new PercentDiscountStrategy().Apply(basePrice: 100m, ruleValue: 15m).Should().Be(85m);

    [Fact]
    public void PercentMarkupStrategy_suma_el_porcentaje_sobre_el_precio_base()
        => new PercentMarkupStrategy().Apply(basePrice: 100m, ruleValue: 10m).Should().Be(110m);

    [Fact]
    public void FixedAdjustmentStrategy_suma_el_monto_fijo_sobre_el_precio_base()
        => new FixedAdjustmentStrategy().Apply(basePrice: 100m, ruleValue: -5m).Should().Be(95m);
}
