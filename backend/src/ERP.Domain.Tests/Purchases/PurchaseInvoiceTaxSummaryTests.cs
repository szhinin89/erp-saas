using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// FLOW-READY-02D.1 — PurchaseInvoice.Confirm() regenera TaxSummaries agrupados por combinación
/// exacta de impuesto (VatCode/VatRate/IceCode/IceRate) desde las líneas ya congeladas. Nunca
/// recalcula desde catálogos vivos ni reimplementa SriTaxCalculator — solo suma snapshots.
/// </summary>
public sealed class PurchaseInvoiceTaxSummaryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WhId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraftInvoice() =>
        PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );

    private static PurchaseInvoiceDetail CreateLine(
        Guid invoiceId,
        string description,
        decimal quantity,
        decimal unitPrice,
        string vatCode,
        string? iceCode = null
    ) =>
        PurchaseInvoiceDetail.Create(
            invoiceId,
            TenantId,
            description,
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT",
            itemId: Guid.NewGuid(),
            warehouseId: WhId,
            iceCode: iceCode
        );

    [Fact]
    public void Confirm_genera_un_resumen_por_grupo_de_impuesto()
    {
        var inv = CreateDraftInvoice();
        var line = CreateLine(inv.Id, "Producto A", 2, 50m, "10");
        inv.ReplaceLines([line], UserId);
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);

        inv.Confirm(UserId);

        inv.TaxSummaries.Should().ContainSingle();
        var summary = inv.TaxSummaries.Single();
        summary.VatCode.Should().Be("10");
        summary.VatRate.Should().Be(15m);
        summary.PurchaseInvoiceId.Should().Be(inv.Id);
    }

    [Fact]
    public void Varias_lineas_con_mismo_impuesto_se_agrupan_en_un_solo_resumen()
    {
        var inv = CreateDraftInvoice();
        var line1 = CreateLine(inv.Id, "Producto A", 2, 50m, "10");
        var line2 = CreateLine(inv.Id, "Producto B", 3, 20m, "10");
        inv.ReplaceLines([line1, line2], UserId);
        line1.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        line2.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);

        inv.Confirm(UserId);

        inv.TaxSummaries.Should().ContainSingle();
        var summary = inv.TaxSummaries.Single();
        summary.TaxableBase.Should().Be(line1.TaxableBase + line2.TaxableBase);
        summary.VatAmount.Should().Be(line1.VatAmount + line2.VatAmount);
    }

    [Fact]
    public void Lineas_con_distinto_VatCode_no_se_mezclan()
    {
        var inv = CreateDraftInvoice();
        var line1 = CreateLine(inv.Id, "Producto A", 1, 100m, "10");
        var line2 = CreateLine(inv.Id, "Producto B", 1, 100m, "0");
        inv.ReplaceLines([line1, line2], UserId);
        line1.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        line2.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);

        inv.Confirm(UserId);

        inv.TaxSummaries.Should().HaveCount(2);
        inv.TaxSummaries.Select(s => s.VatCode).Should().BeEquivalentTo(new[] { "10", "0" });
        var summary10 = inv.TaxSummaries.Single(s => s.VatCode == "10");
        summary10.TaxableBase.Should().Be(line1.TaxableBase);
        var summary0 = inv.TaxSummaries.Single(s => s.VatCode == "0");
        summary0.TaxableBase.Should().Be(line2.TaxableBase);
    }

    [Fact]
    public void Lineas_con_distinto_IceCode_no_se_mezclan()
    {
        var inv = CreateDraftInvoice();
        var line1 = CreateLine(inv.Id, "Producto con ICE A", 1, 100m, "10", iceCode: "3023");
        var line2 = CreateLine(inv.Id, "Producto con ICE B", 1, 100m, "10", iceCode: "3033");
        inv.ReplaceLines([line1, line2], UserId);
        line1.ApplyTaxes("10", 15m, "IVA 15%", "3023", 10m, "ICE Bebidas");
        line2.ApplyTaxes("10", 15m, "IVA 15%", "3033", 20m, "ICE Cigarrillos");

        inv.Confirm(UserId);

        inv.TaxSummaries.Should().HaveCount(2);
        inv.TaxSummaries.Select(s => s.IceCode).Should().BeEquivalentTo(new[] { "3023", "3033" });
    }

    [Fact]
    public void TotalAmount_es_TaxableBase_mas_IceAmount_mas_VatAmount()
    {
        var inv = CreateDraftInvoice();
        var line = CreateLine(inv.Id, "Producto con ICE", 1, 100m, "10", iceCode: "3023");
        inv.ReplaceLines([line], UserId);
        line.ApplyTaxes("10", 15m, "IVA 15%", "3023", 10m, "ICE Bebidas");

        inv.Confirm(UserId);

        var summary = inv.TaxSummaries.Single();
        summary.TotalAmount.Should().Be(summary.TaxableBase + summary.IceAmount + summary.VatAmount);
        summary.IceAmount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Usa_SnapshotVatName_y_SnapshotIceName_de_las_lineas()
    {
        var inv = CreateDraftInvoice();
        var line = CreateLine(inv.Id, "Producto con ICE", 1, 100m, "10", iceCode: "3023");
        inv.ReplaceLines([line], UserId);
        line.ApplyTaxes("10", 15m, "IVA 15%", "3023", 10m, "ICE Bebidas Azucaradas");

        inv.Confirm(UserId);

        var summary = inv.TaxSummaries.Single();
        summary.VatName.Should().Be("IVA 15%");
        summary.IceName.Should().Be("ICE Bebidas Azucaradas");
    }

    [Fact]
    public void No_cambia_los_totales_existentes_de_PurchaseInvoice()
    {
        var inv = CreateDraftInvoice();
        var line1 = CreateLine(inv.Id, "Producto A", 2, 50m, "10");
        var line2 = CreateLine(inv.Id, "Producto B", 1, 30m, "0");
        inv.ReplaceLines([line1, line2], UserId);
        line1.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        line2.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);

        var expectedSubtotal = line1.LineSubtotal + line2.LineSubtotal;
        var expectedTax = line1.VatAmount + line1.IceAmount + line2.VatAmount + line2.IceAmount;

        inv.Confirm(UserId);

        inv.ConfirmedSubtotal.Should().Be(expectedSubtotal);
        inv.ConfirmedTotalTax.Should().Be(expectedTax);
    }

    [Fact]
    public void Compra_sin_ICE_genera_IceAmount_cero()
    {
        var inv = CreateDraftInvoice();
        var line = CreateLine(inv.Id, "Producto sin ICE", 1, 100m, "10");
        inv.ReplaceLines([line], UserId);
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);

        inv.Confirm(UserId);

        var summary = inv.TaxSummaries.Single();
        summary.IceCode.Should().BeNull();
        summary.IceAmount.Should().Be(0m);
    }

    [Fact]
    public void Los_resumenes_heredan_TenantId_CompanyId_BranchId_de_la_factura()
    {
        var inv = CreateDraftInvoice();
        var line = CreateLine(inv.Id, "Producto A", 1, 100m, "10");
        inv.ReplaceLines([line], UserId);
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);

        inv.Confirm(UserId);

        var summary = inv.TaxSummaries.Single();
        summary.TenantId.Should().Be(TenantId);
        summary.CompanyId.Should().Be(CompanyId);
        summary.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public void PurchaseInvoiceTaxSummary_no_expone_factory_publica()
    {
        var method = typeof(PurchaseInvoiceTaxSummary).GetMethod(
            "Create",
            System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
        );

        method.Should().NotBeNull();
        method!.IsAssembly.Should().BeTrue("Create() debe ser internal — solo PurchaseInvoice puede construir summaries");
    }
}
