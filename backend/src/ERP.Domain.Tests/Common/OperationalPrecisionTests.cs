using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Common;

/// <summary>
/// ERP-PRECISION-OPERATIONAL-05B — las escalas operativas (cantidad base, costo unitario) ya no
/// son constantes de FiscalPrecision: Application las resuelve desde CompanyPrecisionPolicy y las
/// pasa al dominio. Sin argumento, el dominio conserva exactamente el comportamiento histórico
/// (regresión).
/// </summary>
public sealed class OperationalPrecisionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    // ── Sales: QuantityInBaseUom ────────────────────────────────────────────

    private static SalesInvoiceDetail SalesLine(decimal quantity, decimal factor, int? quantityDecimals) =>
        quantityDecimals is null
            ? SalesInvoiceDetail.Create(Guid.NewGuid(), TenantId, "Prod", quantity, 1m, "10", "UNIT", conversionFactor: factor)
            : SalesInvoiceDetail.Create(
                Guid.NewGuid(),
                TenantId,
                "Prod",
                quantity,
                1m,
                "10",
                "UNIT",
                conversionFactor: factor,
                quantityDecimals: quantityDecimals.Value
            );

    [Fact]
    public void Sales_sin_precision_configurada_conserva_4_decimales_historicos()
    {
        var line = SalesLine(1.1234567m, 1m, null);

        line.QuantityInBaseUom.Should().Be(1.1235m);
        FiscalPrecision.Quantity.Should().Be(4);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1.12)]
    [InlineData(6, 1.123457)]
    public void Sales_QuantityInBaseUom_respeta_quantityDecimals_configurado(int decimals, double expected)
    {
        var line = SalesLine(1.1234567m, 1m, decimals);

        line.QuantityInBaseUom.Should().Be((decimal)expected);
        // La cantidad capturada nunca se altera — solo la cantidad convertida a unidad base.
        line.Quantity.Should().Be(1.1234567m);
    }

    // ── Purchases: QuantityInBaseUom + LandedUnitCost ───────────────────────

    private static PurchaseInvoiceDetail PurchaseLine(int? quantityDecimals = null, int? unitCostDecimals = null) =>
        PurchaseInvoiceDetail.Create(
            Guid.NewGuid(),
            TenantId,
            "Prod",
            1m,
            1m,
            "10",
            "CAJA",
            conversionFactor: 3m,
            quantityDecimals: quantityDecimals ?? FiscalPrecision.Quantity,
            unitCostDecimals: unitCostDecimals ?? FiscalPrecision.UnitCost
        );

    [Fact]
    public void Purchase_sin_precision_configurada_conserva_LandedUnitCost_de_6_decimales()
    {
        var line = PurchaseLine();

        line.QuantityInBaseUom.Should().Be(3m);
        line.LandedUnitCost.Should().Be(0.333333m);
    }

    [Theory]
    [InlineData(2, 0.33)]
    [InlineData(10, 0.3333333333)]
    public void Purchase_LandedUnitCost_respeta_unitCostDecimals_configurado(int decimals, double expected)
    {
        var line = PurchaseLine(unitCostDecimals: decimals);

        line.LandedUnitCost.Should().Be((decimal)expected);
        // TotalLineCost es derivado: conserva su escala fija (FiscalPrecision.UnitCost).
        line.TotalLineCost.Should().Be(1m);
    }

    [Fact]
    public void Purchase_LandedUnitCost_se_recalcula_con_la_precision_tras_ApplyDiscount_y_ApplyUnitCostPrecision()
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Proveedor",
            "1234567890001",
            "01",
            "001-001-000000123",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );
        inv.ReplaceLines([PurchaseLine()], Guid.NewGuid());
        inv.Lines[0].LandedUnitCost.Should().Be(0.333333m);

        // Application carga el agregado y fija la escala de la empresa antes de operar.
        inv.ApplyUnitCostPrecision(10);
        inv.Lines[0].LandedUnitCost.Should().Be(0.3333333333m);

        // Un recálculo posterior (descuento global) respeta la escala fijada.
        inv.ApplyGlobalDiscount(10m, Guid.NewGuid());
        inv.Lines[0].LandedUnitCost.Should().Be(0.3m);
    }

    // ── Inventory: StockAdjustmentLine ──────────────────────────────────────

    private static StockAdjustmentLine AdjustmentLine(int? quantityDecimals, int? unitCostDecimals) =>
        StockAdjustmentLine.Create(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            "Item",
            null,
            "UNIT",
            "UNIT",
            1m,
            1.1234567m,
            1.123456789012m,
            null,
            0,
            default,
            quantityDecimals ?? FiscalPrecision.Quantity,
            unitCostDecimals ?? FiscalPrecision.UnitCost
        );

    [Fact]
    public void StockAdjustmentLine_sin_precision_configurada_conserva_4_y_6_decimales()
    {
        var line = AdjustmentLine(null, null);

        line.QuantityInBaseUom.Should().Be(1.1235m);
        line.UnitCostBase.Should().Be(1.123457m);
    }

    [Fact]
    public void StockAdjustmentLine_respeta_quantityDecimals_y_unitCostDecimals_configurados()
    {
        var line = AdjustmentLine(6, 10);

        line.QuantityInBaseUom.Should().Be(1.123457m);
        line.UnitCostBase.Should().Be(1.1234567890m);
    }

    [Fact]
    public void StockAdjustmentLine_ApplyExecutionResult_redondea_el_costo_resuelto_a_la_escala_recibida()
    {
        var line = AdjustmentLine(6, 10);

        line.ApplyExecutionResult(0m, 1.123457m, 2.123456789012m, unitCostDecimals: 8);

        line.UnitCostBase.Should().Be(2.12345679m);
    }
}
