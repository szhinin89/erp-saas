using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// FLOW-READY-02F.1 — modelo de impuestos múltiples por línea de compra: ICE "específico" (monto
/// fijo del XML, nunca recalculado desde una tarifa porcentual) e IRBPNR (siempre derivado de
/// <see cref="PurchaseInvoiceDetail.Taxes"/>, nunca tratado como ICE).
/// </summary>
public sealed class PurchaseInvoiceDetailMultiTaxTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static PurchaseInvoiceDetail CreateLine(decimal quantity, decimal unitPrice, string vatCode) =>
        PurchaseInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: TenantId,
            description: "Fanta Harmony NRJ 1350 PET(12)",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT"
        );

    [Fact]
    public void Create_con_presentacion_de_compra_calcula_cantidad_y_costo_en_unidad_base()
    {
        var line = PurchaseInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: TenantId,
            description: "Fanta Harmony NRJ 1350 PET(12)",
            quantity: 2m,
            unitPrice: 9.29m,
            vatCode: "4",
            uomCode: "PACA",
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );

        line.Quantity.Should().Be(2m);
        line.UomCode.Should().Be("PACA");
        line.BaseUomCode.Should().Be("UNIT");
        line.ConversionFactor.Should().Be(12m);
        line.QuantityInBaseUom.Should().Be(24m);
        line.LineSubtotal.Should().Be(18.58m);
        line.LandedUnitCost.Should().Be(0.774167m);
    }

    [Fact]
    public void ApplyTaxes_con_ICE_Percentage_mantiene_comportamiento_historico()
    {
        var line = CreateLine(1, 100m, "4");

        line.ApplyTaxes("4", 4m, "IVA 4%", "3041", 10m, "Bebidas gaseosas con azúcar añadida");

        line.IceCalculationType.Should().Be(SriTaxCalculationType.Percentage);
        line.IceAmount.Should().Be(10m); // 100 * 10/100
        line.VatAmount.Should().Be(4.40m); // (100+10) * 4/100 = 4.40
    }

    [Fact]
    public void ApplyTaxes_con_ICE_Specific_fija_el_monto_exacto_del_XML_sin_recalcularlo()
    {
        var line = CreateLine(24, 0.5837m, "4"); // 24 unidades reales (2 pacas x 12), Arca Continental
        const decimal iceExactoDelXml = 1.23m;

        line.ApplyTaxes(
            "4",
            4m,
            "IVA 4%",
            "3053",
            0m,
            "Bebidas gaseosas con alto contenido de azúcar",
            SriTaxCalculationType.Specific,
            iceExactoDelXml
        );

        line.IceCalculationType.Should().Be(SriTaxCalculationType.Specific);
        line.IceAmount.Should().Be(iceExactoDelXml, "un impuesto específico nunca se recalcula desde una tarifa porcentual");
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

        // Regla SRI: IVA se calcula sobre (base + ICE), sin importar cómo se determinó el ICE.
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

        line.IceAmount.Should().Be(5m, "un impuesto específico (por botella/por 100g de azúcar) no es proporcional al descuento de la línea");
    }

    [Fact]
    public void TaxInclusiveTotal_incluye_IrbpnrAmount_FLOW_READY_02F_2()
    {
        // FLOW-READY-02F.2 — IRBPNR forma parte del valor real del XML y de la cuenta por pagar al
        // proveedor: debe estar incluido en TaxInclusiveTotal (y por lo tanto en GrandTotal).
        var line = CreateLine(24, 0.5837m, "4");
        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);
        line.ReplaceTaxes(
            [
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.02m,
                    SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    0.48m,
                    PurchaseTaxSource.Xml
                ),
            ]
        );

        line.TaxInclusiveTotal.Should()
            .Be(line.TaxableBase + line.IceAmount + line.VatAmount + line.IrbpnrAmount);
        line.TaxInclusiveTotal.Should().BeGreaterThan(line.TaxableBase + line.VatAmount);
    }

    [Fact]
    public void IrbpnrAmount_es_cero_sin_Taxes_asociadas()
    {
        var line = CreateLine(1, 100m, "4");
        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);

        line.IrbpnrAmount.Should().Be(0m);
        line.IrbpnrCode.Should().BeNull();
        // TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3): ApplyTaxes sincroniza siempre la fila de IVA
        // hacia Taxes (antes solo ocurría en líneas de Recepción Electrónica) — sin ICE ni IRBPNR,
        // Taxes contiene únicamente esa fila de IVA, nunca vacía.
        line.Taxes.Should().ContainSingle(t => t.TaxCode == "2");
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
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "2",
                    "4",
                    "IVA 4%",
                    4m,
                    SriTaxCalculationType.Percentage,
                    line.TaxableBase,
                    line.VatAmount,
                    PurchaseTaxSource.Xml
                ),
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "3",
                    "3053",
                    "ICE Específico",
                    null,
                    SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    1.23m,
                    PurchaseTaxSource.Xml
                ),
                PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.02m,
                    SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    0.48m,
                    PurchaseTaxSource.Xml
                ),
            ]
        );

        line.IrbpnrAmount.Should().Be(0.48m);
        line.IrbpnrCode.Should().Be("5001");
        line.IceAmount.Should().Be(1.23m, "ReplaceTaxes es aditivo y no debe tocar los campos escalares legacy");
        line.Taxes.Should().HaveCount(3);
    }

    [Fact]
    public void ReplaceTaxes_falla_si_la_linea_ya_esta_congelada()
    {
        var line = CreateLine(1, 100m, "4");
        line.ApplyTaxes("4", 4m, "IVA 4%", null, 0m, null);
        typeof(PurchaseInvoiceDetail)
            .GetMethod("FreezeCosts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(line, null);

        var act = () => line.ReplaceTaxes([]);

        act.Should().Throw<InvalidOperationException>();
    }
}
