using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// FLOW-READY-02F.1 — bug de IVA/IRBPNR: <see cref="PurchaseInvoiceDetail.ApplyTaxes"/> siempre
/// recalculaba VatAmount/IceAmount por tarifa de catálogo, incluso cuando la línea venía del XML
/// y ya traía el monto EXACTO del comprobante en <see cref="PurchaseInvoiceDetail.Taxes"/>
/// (poblado vía <see cref="PurchaseInvoiceDetail.ReplaceTaxes"/>). Estos tests verifican que, tras
/// el fix, el snapshot XML (Source=Xml) prevalece sobre el recálculo — en Create/Update/Confirm/
/// DistributeCost por igual, ya que todos pasan por <c>ApplyTaxes</c>.
/// </summary>
public sealed class PurchaseInvoiceDetailXmlTaxSnapshotTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static PurchaseInvoiceDetail CreateLine(
        decimal quantity,
        decimal unitPrice,
        string vatCode
    ) =>
        PurchaseInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: TenantId,
            description: "INCA-KOLA ORGL 900ML PET NR 12",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT"
        );

    [Theory]
    [InlineData(SriTaxCalculationType.Percentage, 9.87)]
    [InlineData(SriTaxCalculationType.Specific, 0)]
    [InlineData(SriTaxCalculationType.Mixed, 18.55)]
    public void Xml_snapshot_preserves_rate_base_and_explicit_amount_after_recalculation(
        SriTaxCalculationType mode, double amount)
    {
        var line = CreateLine(1, 100m, "4");
        line.ReplaceTaxes([
            PurchaseInvoiceDetailTax.Create(line.Id, TenantId, "3", "3031", "ICE", 0.18m,
                mode, 35.81m, (decimal)amount, PurchaseTaxSource.Xml),
            PurchaseInvoiceDetailTax.Create(line.Id, TenantId, "2", "4", "IVA", 12m,
                SriTaxCalculationType.Percentage, 118.55m, 14.23m, PurchaseTaxSource.Xml),
        ]);

        line.ApplyTaxes("4", 15m, "IVA", "3031", 75m, "ICE", mode, 999m);
        line.SetFreightAllocated(5m);
        line.ApplyTaxes("4", 15m, "IVA", "3031", 75m, "ICE", mode, 999m);

        var ice = line.Taxes.Single(t => t.TaxCode == "3");
        ice.Rate.Should().Be(0.18m);
        ice.TaxableBase.Should().Be(35.81m);
        ice.TaxAmount.Should().Be((decimal)amount);
        var vat = line.Taxes.Single(t => t.TaxCode == "2");
        vat.Rate.Should().Be(12m);
        vat.TaxableBase.Should().Be(118.55m);
        vat.TaxAmount.Should().Be(14.23m);
    }

    [Fact]
    public void Manual_mixed_ICE_without_XML_snapshot_is_rejected_instead_of_calculated()
    {
        var line = CreateLine(1, 100m, "4");

        var act = () => line.ApplyTaxes("4", 15m, "IVA", "3031", 75m, "ICE", SriTaxCalculationType.Mixed);

        act.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>().WithMessage("*mixto*XML*");
    }

    [Fact]
    public void ApplyTaxes_prevalece_el_monto_exacto_de_IVA_del_XML_sobre_el_recalculo_por_tarifa()
    {
        // Caso real del ticket: Base 12.98, IVA exacto del XML 1.61 — tarifa de catálogo 15%
        // recalcularía a 12.98 * 15% = 1.947 => redondeado 1.95, que es el bug reportado.
        var line = CreateLine(1, 12.98m, "4");

        line.ReplaceTaxes([
            PurchaseInvoiceDetailTax.Create(
                line.Id,
                TenantId,
                "2",
                "4",
                "IVA 15%",
                15m,
                SriTaxCalculationType.Percentage,
                12.98m,
                1.61m,
                PurchaseTaxSource.Xml
            ),
        ]);

        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);

        line.VatAmount.Should()
            .Be(
                1.61m,
                "el snapshot exacto del XML debe prevalecer sobre el recálculo por tarifa (que daría 1.95)"
            );
    }

    [Fact]
    public void Caso_de_aceptacion_Inca_Kola_Base_IVA_ICE_IRBPNR_suman_el_total_exacto_del_XML()
    {
        // Base 12.98 + IVA 1.61 + ICE 0.00 + IRBPNR 0.24 = Total línea 14.83 (caso exacto del ticket).
        var line = CreateLine(1, 12.98m, "4");

        line.ReplaceTaxes([
            PurchaseInvoiceDetailTax.Create(
                line.Id,
                TenantId,
                "2",
                "4",
                "IVA 15%",
                15m,
                SriTaxCalculationType.Percentage,
                12.98m,
                1.61m,
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
                12.98m,
                0.24m,
                PurchaseTaxSource.Xml
            ),
        ]);

        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);

        line.VatAmount.Should().Be(1.61m);
        line.IceAmount.Should().Be(0m);
        line.IrbpnrAmount.Should().Be(0.24m);
        line.TaxInclusiveTotal.Should().Be(14.83m);
    }

    [Fact]
    public void ApplyTaxes_sin_snapshot_XML_sigue_recalculando_normalmente_lineas_manuales()
    {
        var line = CreateLine(1, 100m, "4");

        // Sin ReplaceTaxes — línea manual, _taxes vacío.
        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);

        line.VatAmount.Should()
            .Be(
                15m,
                "sin snapshot XML el comportamiento histórico (recálculo por tarifa) no debe romperse"
            );
    }

    [Fact]
    public void ApplyTaxes_ignora_snapshot_no_Xml_Calculated_sigue_recalculando()
    {
        var line = CreateLine(1, 100m, "4");

        line.ReplaceTaxes([
            PurchaseInvoiceDetailTax.Create(
                line.Id,
                TenantId,
                "2",
                "4",
                "IVA 15%",
                15m,
                SriTaxCalculationType.Percentage,
                100m,
                9.99m,
                PurchaseTaxSource.Calculated
            ),
        ]);

        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);

        line.VatAmount.Should()
            .Be(
                15m,
                "un snapshot Source=Calculated no es un monto documental exacto del XML — no debe prevalecer"
            );
    }

    [Fact]
    public void ApplyTaxes_prevalece_el_monto_exacto_de_ICE_Percentage_del_XML()
    {
        var line = CreateLine(1, 100m, "4");

        line.ReplaceTaxes([
            PurchaseInvoiceDetailTax.Create(
                line.Id,
                TenantId,
                "3",
                "3041",
                "ICE 10%",
                10m,
                SriTaxCalculationType.Percentage,
                100m,
                9.87m,
                PurchaseTaxSource.Xml
            ),
        ]);

        line.ApplyTaxes("4", 4m, "IVA 4%", "3041", 10m, "ICE 10%");

        line.IceAmount.Should()
            .Be(
                9.87m,
                "el snapshot exacto del XML debe prevalecer sobre el recálculo por tarifa (que daría 10.00)"
            );
    }
}
