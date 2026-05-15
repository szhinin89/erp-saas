using FluentAssertions;
using ERP.Application.Modules.Purchasing.Services;

namespace ERP.API.Tests.Unit;

public sealed class CompraRetencionCalculoTests
{
    [Theory]
    [InlineData(100, 10, 10)]
    [InlineData(50, 1.5, 0.75)]
    [InlineData(6, 30, 1.8)]
    public void CalcularValorRetenido_redondea_a_dos_decimales(decimal @base, decimal pct, decimal esperado)
    {
        CompraRetencionCalculo.CalcularValorRetenido(@base, pct).Should().Be(esperado);
    }

    [Fact]
    public void CalcularValorRetenido_tercer_decimal_usa_redondeo_estandar()
    {
        CompraRetencionCalculo.CalcularValorRetenido(33.333m, 1m).Should().Be(0.33m);
    }
}
