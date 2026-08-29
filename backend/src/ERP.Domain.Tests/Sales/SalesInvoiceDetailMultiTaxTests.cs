using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032) — modelo de impuestos múltiples por línea de venta:
/// SalesInvoiceDetailTax como fuente de verdad, ICE "específico" (paridad con Compras) e IRBPNR
/// (inexistente en Ventas antes de este ADR).
/// </summary>
public sealed class SalesInvoiceDetailMultiTaxTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static SalesInvoiceDetail CreateLine(decimal quantity, decimal unitPrice, string vatCode) =>
        SalesInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: TenantId,
            description: "Fanta Harmony NRJ 1350 PET(12)",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT"
        );

    [Fact]
    public void ApplyTaxes_sincroniza_siempre_la_fila_de_IVA_hacia_Taxes()
    {
        var line = CreateLine(1, 100m, "4");

        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);

        line.Taxes.Should().ContainSingle(t => t.TaxCode == "2" && t.TaxAmount == line.VatAmount);
    }

    [Fact]
    public void ApplyTaxes_con_ICE_Percentage_sincroniza_ambas_filas()
    {
        var line = CreateLine(1, 100m, "4");

        line.ApplyTaxes("4", 4m, "IVA 4%", "3041", 10m, "Bebidas gaseosas con azúcar añadida");

        line.IceCalculationType.Should().Be(SriTaxCalculationType.Percentage);
        line.IceAmount.Should().Be(10m); // 100 * 10/100
        line.VatAmount.Should().Be(4.40m); // (100+10) * 4/100 = 4.40
        line.Taxes.Should().HaveCount(2);
        line.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxAmount == 10m);
    }

    [Fact]
    public void ApplyTaxes_con_ICE_Specific_fija_el_monto_exacto_sin_recalcularlo()
    {
        var line = CreateLine(24, 0.5837m, "4");
        const decimal iceEspecifico = 1.23m;

        line.ApplyTaxes(
            "4",
            4m,
            "IVA 4%",
            "3053",
            0m,
            "Bebidas gaseosas con alto contenido de azúcar",
            SriTaxCalculationType.Specific,
            iceEspecifico
        );

        line.IceCalculationType.Should().Be(SriTaxCalculationType.Specific);
        line.IceAmount.Should().Be(iceEspecifico, "un impuesto específico nunca se recalcula desde una tarifa porcentual");
        line.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxAmount == iceEspecifico);
    }

    [Fact]
    public void ICE_Specific_se_incluye_en_la_base_del_IVA_igual_que_ICE_Percentage()
    {
        var line = CreateLine(1, 100m, "4");

        line.ApplyTaxes(
            "4",
            4m,
            "IVA 4%",
            "3053",
            0m,
            "ICE Específico",
            SriTaxCalculationType.Specific,
            iceExactAmount: 5m
        );

        line.VatAmount.Should().Be(4.20m); // (100 + 5) * 4/100 = 4.20
    }

    [Fact]
    public void ApplyDiscount_no_recalcula_un_ICE_Specific_ya_fijado()
    {
        var line = CreateLine(1, 100m, "4");
        line.ApplyTaxes(
            "4",
            4m,
            "IVA 4%",
            "3053",
            0m,
            "ICE Específico",
            SriTaxCalculationType.Specific,
            iceExactAmount: 5m
        );

        line.ApplyDiscount(10m);

        line.IceAmount.Should().Be(5m);
        line.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxAmount == 5m);
    }

    [Fact]
    public void TaxInclusiveTotal_incluye_IrbpnrAmount()
    {
        var line = CreateLine(24, 0.5837m, "4");
        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);
        line.ReplaceTaxes(
            [
                SalesInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.02m,
                    SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    0.48m,
                    SalesTaxSource.Calculated
                ),
            ]
        );

        line.TaxInclusiveTotal.Should()
            .Be(line.TaxableBase + line.IceAmount + line.VatAmount + line.IrbpnrAmount);
        line.TaxInclusiveTotal.Should().BeGreaterThan(line.TaxableBase + line.VatAmount);
    }

    [Fact]
    public void IrbpnrAmount_es_cero_sin_fila_IRBPNR()
    {
        var line = CreateLine(1, 100m, "4");
        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);

        line.IrbpnrAmount.Should().Be(0m);
        line.IrbpnrCode.Should().BeNull();
    }

    [Fact]
    public void IrbpnrAmount_se_deriva_de_Taxes_y_nunca_se_confunde_con_ICE()
    {
        var line = CreateLine(24, 0.5837m, "4");
        line.ApplyTaxes(
            "4",
            4m,
            "IVA 4%",
            "3053",
            0m,
            "ICE Específico",
            SriTaxCalculationType.Specific,
            iceExactAmount: 1.23m
        );

        line.ReplaceTaxes(
            [
                SalesInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.02m,
                    SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    0.48m,
                    SalesTaxSource.Calculated
                ),
            ]
        );

        line.IrbpnrAmount.Should().Be(0.48m);
        line.IrbpnrCode.Should().Be("5001");
        line.IceAmount.Should().Be(1.23m, "ReplaceTaxes no debe tocar la fila de ICE administrada por ApplyTaxes");
        line.Taxes.Should().HaveCount(3, "IVA + ICE (sincronizados por ApplyTaxes) + IRBPNR (ReplaceTaxes)");
    }

    [Fact]
    public void ReplaceTaxes_falla_si_la_linea_ya_esta_autorizada()
    {
        var line = CreateLine(1, 100m, "4");
        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);
        typeof(SalesInvoiceDetail)
            .GetMethod("Freeze", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(line, null);

        var act = () => line.ReplaceTaxes([]);

        act.Should().Throw<InvalidOperationException>();
    }
}
