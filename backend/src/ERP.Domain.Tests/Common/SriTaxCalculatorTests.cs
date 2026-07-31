using ERP.Domain.Common;
using FluentAssertions;

namespace ERP.Domain.Tests.Common;

/// <summary>
/// Cobertura directa de la autoridad única de cálculo tributario SRI (P1-02,
/// ERP_CORE_SUMAK_READINESS_AUDIT.md): antes de este fix, Sales invocaba este algoritmo vía
/// SriTaxCalculator.Compute mientras Purchases lo reimplementaba manualmente en
/// PurchaseInvoiceDetail.RecalcTaxes(). Ambos módulos consumen ahora esta única función.
/// </summary>
public sealed class SriTaxCalculatorTests
{
    [Fact]
    public void Sin_ICE_calcula_IVA_solo_sobre_la_base_imponible()
    {
        var (ice, vat, taxInclusive) = SriTaxCalculator.Compute(
            taxableBase: 100m,
            vatRate: 15m,
            iceRate: 0m
        );

        ice.Should().Be(0m);
        vat.Should().Be(15m);
        taxInclusive.Should().Be(115m);
    }

    [Fact]
    public void Con_ICE_el_IVA_se_calcula_sobre_base_mas_ICE()
    {
        // ICE = 100 * 10% = 10 ; base IVA = 110 ; IVA = 110 * 15% = 16.50
        var (ice, vat, taxInclusive) = SriTaxCalculator.Compute(
            taxableBase: 100m,
            vatRate: 15m,
            iceRate: 10m
        );

        ice.Should().Be(10m);
        vat.Should().Be(16.50m);
        taxInclusive.Should().Be(126.50m);
    }

    [Fact]
    public void Tasa_IVA_cero_no_genera_IVA_aunque_haya_ICE()
    {
        var (ice, vat, taxInclusive) = SriTaxCalculator.Compute(
            taxableBase: 50m,
            vatRate: 0m,
            iceRate: 20m
        );

        ice.Should().Be(10m);
        vat.Should().Be(0m);
        taxInclusive.Should().Be(60m);
    }

    [Fact]
    public void Tasa_ICE_negativa_o_cero_se_trata_como_sin_ICE()
    {
        var (ice, vat, _) = SriTaxCalculator.Compute(taxableBase: 100m, vatRate: 15m, iceRate: 0m);

        ice.Should().Be(0m);
        vat.Should().Be(15m);
    }

    [Theory]
    [InlineData(1, 12.5, 0, 0.13)] // 1 * 12.5 / 100 = 0.125 → punto medio exacto → AwayFromZero → 0.13
    [InlineData(200, 15, 0, 30.00)]
    public void Redondea_montos_a_2_decimales_AwayFromZero(
        decimal taxableBase,
        decimal vatRate,
        decimal iceRate,
        decimal expectedVat
    )
    {
        var (_, vat, _) = SriTaxCalculator.Compute(taxableBase, vatRate, iceRate);

        vat.Should().Be(expectedVat);
    }
}
