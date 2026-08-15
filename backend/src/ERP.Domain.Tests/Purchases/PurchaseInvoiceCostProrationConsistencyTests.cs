using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// PURCHASE-LANDED-COST-PRORATION-CONSISTENCY-01 — DistributeAdditionalCost (aditivo, subconjunto
/// de líneas) y ProrateCostToLines/DistributeCosts (absoluto, todas las líneas) comparten ahora la
/// misma lógica interna de prorrateo (ProrateAmount): misma precisión (FiscalPrecision.UnitCost),
/// mismo redondeo (AwayFromZero), última línea absorbe el residuo, y fallback de reparto
/// equitativo (en vez de excepción) cuando la base imponible total es &lt;= 0.
/// </summary>
public sealed class PurchaseInvoiceCostProrationConsistencyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraftWithLines(params (decimal qty, decimal price)[] lines)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );

        var details = lines
            .Select(l => PurchaseInvoiceDetail.Create(
                inv.Id,
                TenantId,
                "Producto",
                l.qty,
                l.price,
                "10",
                "UNIT"
            ))
            .ToList();
        inv.ReplaceLines(details, UserId);
        return inv;
    }

    // ── Caso 1/2: ambas rutas usan la misma precisión y producen el mismo resultado ─────────

    [Fact]
    public void DistributeAdditionalCost_y_DistributeCosts_producen_el_mismo_prorrateo_para_el_mismo_escenario()
    {
        // Línea A: 100, Línea B: 300, Línea C: 233.33 (fuerza decimales no exactos) → flete 77.77.
        var invAdditional = CreateDraftWithLines((1m, 100m), (1m, 300m), (1m, 233.33m));
        var idsAdditional = invAdditional.Lines.Select(l => l.Id).ToArray();
        invAdditional.DistributeAdditionalCost(PurchaseCostType.Freight, 77.77m, idsAdditional, UserId);

        var invAbsolute = CreateDraftWithLines((1m, 100m), (1m, 300m), (1m, 233.33m));
        invAbsolute.DistributeCosts(77.77m, 0m, UserId);

        for (var i = 0; i < 3; i++)
            invAdditional.Lines[i].FreightAllocated.Should().Be(invAbsolute.Lines[i].FreightAllocated);

        invAdditional.Lines.Sum(l => l.FreightAllocated).Should().Be(77.77m);
        invAbsolute.Lines.Sum(l => l.FreightAllocated).Should().Be(77.77m);
    }

    [Fact]
    public void DistributeCosts_recalcula_LandedUnitCost()
    {
        var inv = CreateDraftWithLines((10m, 10m)); // subtotal 100, qty base 10
        inv.DistributeCosts(20m, 0m, UserId);

        inv.Lines[0].LandedUnitCost.Should().Be(12m); // (100 + 20) / 10
    }

    // ── Caso 3: última línea absorbe el residual en ambas rutas ─────────────────────────────

    [Fact]
    public void DistributeCosts_ultima_linea_absorbe_el_residuo_suma_exacta_al_total()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m), (1m, 100m));

        inv.DistributeCosts(10m, 0m, UserId);

        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(10m);
    }

    // ── Caso 4: totalBase <= 0 con líneas elegibles — fallback equitativo, sin excepción ────

    [Fact]
    public void DistributeCosts_con_base_total_cero_reparte_equitativamente_sin_lanzar()
    {
        var inv = CreateDraftWithLines((1m, 0m), (1m, 0m), (1m, 0m));

        var act = () => inv.DistributeCosts(10m, 0m, UserId);

        act.Should().NotThrow();
        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(10m);
        inv.Lines[0].FreightAllocated.Should().Be(inv.Lines[1].FreightAllocated);
    }

    // ── Caso 5: sin líneas elegibles — DistributeAdditionalCost mantiene excepción clara ────

    [Fact]
    public void DistributeAdditionalCost_sin_lineas_incluidas_lanza_excepcion_clara()
    {
        var inv = CreateDraftWithLines((1m, 100m));

        var act = () => inv.DistributeAdditionalCost(
            PurchaseCostType.Freight,
            10m,
            Array.Empty<Guid>(),
            UserId
        );

        act.Should().Throw<ArgumentException>().WithParameterName("includedLineIds");
    }

    // ── Caso 6: Freight y OtherCost no se mezclan ────────────────────────────────────────────

    [Fact]
    public void DistributeCosts_asigna_Freight_y_OtherCost_a_campos_separados_sin_mezclarlos()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 300m));

        inv.DistributeCosts(40m, 8m, UserId);

        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(40m);
        inv.Lines.Sum(l => l.OtherCostsAllocated).Should().Be(8m);
        // Verificación cruzada: ningún monto de flete terminó en OtherCostsAllocated ni viceversa.
        inv.Lines[0].FreightAllocated.Should().NotBe(inv.Lines[0].OtherCostsAllocated);
    }

    [Fact]
    public void DistributeAdditionalCost_de_Freight_no_toca_OtherCostsAllocated()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var ids = new[] { inv.Lines[0].Id };
        inv.DistributeAdditionalCost(PurchaseCostType.OtherCost, 5m, ids, UserId);

        inv.DistributeAdditionalCost(PurchaseCostType.Freight, 10m, ids, UserId);

        inv.Lines[0].FreightAllocated.Should().Be(10m);
        inv.Lines[0].OtherCostsAllocated.Should().Be(5m); // sin cambios por la distribución de Freight
    }

    // ── Caso 7: distribuir costo adicional no altera impuestos ──────────────────────────────

    [Fact]
    public void DistributeAdditionalCost_no_cambia_TaxableBase_VatAmount_IceAmount_IrbpnrAmount()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var line = inv.Lines[0];
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
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

        var taxableBaseBefore = line.TaxableBase;
        var vatAmountBefore = line.VatAmount;
        var iceAmountBefore = line.IceAmount;
        var irbpnrAmountBefore = line.IrbpnrAmount;

        inv.DistributeAdditionalCost(PurchaseCostType.Freight, 20m, new[] { line.Id }, UserId);

        line.TaxableBase.Should().Be(taxableBaseBefore);
        line.VatAmount.Should().Be(vatAmountBefore);
        line.IceAmount.Should().Be(iceAmountBefore);
        line.IrbpnrAmount.Should().Be(irbpnrAmountBefore);
    }

    [Fact]
    public void DistributeCosts_no_cambia_TaxableBase_VatAmount_IceAmount()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var line = inv.Lines[0];
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);

        var taxableBaseBefore = line.TaxableBase;
        var vatAmountBefore = line.VatAmount;
        var iceAmountBefore = line.IceAmount;

        inv.DistributeCosts(20m, 5m, UserId);

        line.TaxableBase.Should().Be(taxableBaseBefore);
        line.VatAmount.Should().Be(vatAmountBefore);
        line.IceAmount.Should().Be(iceAmountBefore);
    }
}
