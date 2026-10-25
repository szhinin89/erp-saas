using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// PURCHASE-LANDED-COST-PRORATION-CONSISTENCY-01 — toda distribución canónica usa la única lógica de
/// prorrateo (ProrateAmount): precisión FiscalPrecision.UnitCost, redondeo AwayFromZero, la última
/// línea absorbe el residuo y fallback equitativo (sin excepción) cuando la base total es &lt;= 0.
/// </summary>
public sealed class PurchaseInvoiceCostProrationConsistencyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Guid[] AllIds(PurchaseInvoice inv) => inv.Lines.Select(l => l.Id).ToArray();

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
            .Select(l =>
                PurchaseInvoiceDetail.Create(
                    inv.Id,
                    TenantId,
                    "Producto",
                    l.qty,
                    l.price,
                    "10",
                    "UNIT",
                    itemId: Guid.NewGuid(),
                    warehouseId: Guid.NewGuid()
                )
            )
            .ToList();
        inv.ReplaceLines(details, UserId);
        return inv;
    }

    [Fact]
    public void Confirmation_event_uses_frozen_costing_with_discount_and_selected_freight()
    {
        var inv = CreateDraftWithLines((2m, 50m), (1m, 100m));
        inv.ApplyGlobalDiscount(10m, UserId);
        inv.Distribute(PurchaseCostType.Freight, 12m, [inv.Lines[0].Id], UserId);
        inv.Distribute(PurchaseCostType.OtherCost, 3m, [inv.Lines[1].Id], UserId);
        inv.Confirm(UserId);
        var e = inv
            .DomainEvents.OfType<ERP.Domain.Modules.Purchases.Events.PurchaseInvoiceConfirmedEvent>()
            .Single();
        e.Subtotal.Should().Be(200m);
        e.TotalDiscount.Should().Be(20m);
        e.CostSubtotal.Should().Be(195m);
        e.CostSubtotal.Should().Be(inv.Lines.Sum(l => l.TotalLineCost));
        e.GrandTotal.Should().Be(195m);
        inv.Lines[0].LandedUnitCost.Should().Be(51m);
        inv.Lines[1].LandedUnitCost.Should().Be(93m);
    }

    // ── Caso 1/2: prorrateo proporcional exacto y costo unitario recalculado ─────────────────

    [Fact]
    public void Distribucion_prorratea_proporcional_a_la_base_y_suma_exacta()
    {
        // Línea A: 100, Línea B: 300, Línea C: 233.33 (fuerza decimales no exactos) → flete 77.77.
        var inv = CreateDraftWithLines((1m, 100m), (1m, 300m), (1m, 233.33m));
        inv.Distribute(PurchaseCostType.Freight, 77.77m, AllIds(inv), UserId);

        inv.Lines.Select(l => l.FreightAllocated).Should().Equal(12.279538m, 36.838615m, 28.651847m);
        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(77.77m);
    }

    [Fact]
    public void Distribucion_recalcula_LandedUnitCost()
    {
        var inv = CreateDraftWithLines((10m, 10m)); // subtotal 100, qty base 10
        inv.Distribute(PurchaseCostType.Freight, 20m, AllIds(inv), UserId);

        inv.Lines[0].LandedUnitCost.Should().Be(12m); // (100 + 20) / 10
    }

    // ── Caso 3: última línea absorbe el residual en ambas rutas ─────────────────────────────

    [Fact]
    public void Distribucion_ultima_linea_absorbe_el_residuo_suma_exacta_al_total()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m), (1m, 100m));

        inv.Distribute(PurchaseCostType.Freight, 10m, AllIds(inv), UserId);

        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(10m);
    }

    // ── Caso 4: totalBase <= 0 con líneas elegibles — fallback equitativo, sin excepción ────

    [Fact]
    public void Distribucion_con_base_total_cero_reparte_equitativamente_sin_lanzar()
    {
        var inv = CreateDraftWithLines((1m, 0m), (1m, 0m), (1m, 0m));

        var act = () => inv.Distribute(PurchaseCostType.Freight, 10m, AllIds(inv), UserId);

        act.Should().NotThrow();
        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(10m);
        inv.Lines[0].FreightAllocated.Should().Be(inv.Lines[1].FreightAllocated);
    }

    // ── Caso 5: sin líneas destino — excepción de dominio clara ─────────────────────────────

    [Fact]
    public void Distribucion_sin_lineas_destino_lanza_excepcion_clara()
    {
        var inv = CreateDraftWithLines((1m, 100m));

        var act = () => inv.Distribute(PurchaseCostType.Freight, 10m, Array.Empty<Guid>(), UserId);

        act.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>();
    }

    // ── Caso 6: Freight y OtherCost no se mezclan ────────────────────────────────────────────

    [Fact]
    public void Distribucion_asigna_Freight_y_OtherCost_a_campos_separados_sin_mezclarlos()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 300m));

        inv.Distribute(PurchaseCostType.Freight, 40m, AllIds(inv), UserId);
        inv.Distribute(PurchaseCostType.OtherCost, 8m, AllIds(inv), UserId);

        inv.Lines.Sum(l => l.FreightAllocated).Should().Be(40m);
        inv.Lines.Sum(l => l.OtherCostsAllocated).Should().Be(8m);
        // Verificación cruzada: ningún monto de flete terminó en OtherCostsAllocated ni viceversa.
        inv.Lines[0].FreightAllocated.Should().NotBe(inv.Lines[0].OtherCostsAllocated);
    }

    [Fact]
    public void Distribucion_de_Freight_no_toca_OtherCostsAllocated()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var ids = new[] { inv.Lines[0].Id };
        inv.Distribute(PurchaseCostType.OtherCost, 5m, ids, UserId);

        inv.Distribute(PurchaseCostType.Freight, 10m, ids, UserId);

        inv.Lines[0].FreightAllocated.Should().Be(10m);
        inv.Lines[0].OtherCostsAllocated.Should().Be(5m); // sin cambios por la distribución de Freight
    }

    // ── Caso 7: distribuir costo adicional no altera impuestos ──────────────────────────────

    [Fact]
    public void Distribucion_no_cambia_TaxableBase_VatAmount_IceAmount_IrbpnrAmount()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var line = inv.Lines[0];
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        line.ReplaceTaxes([
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
        ]);

        var taxableBaseBefore = line.TaxableBase;
        var vatAmountBefore = line.VatAmount;
        var iceAmountBefore = line.IceAmount;
        var irbpnrAmountBefore = line.IrbpnrAmount;

        inv.Distribute(PurchaseCostType.Freight, 20m, new[] { line.Id }, UserId);

        line.TaxableBase.Should().Be(taxableBaseBefore);
        line.VatAmount.Should().Be(vatAmountBefore);
        line.IceAmount.Should().Be(iceAmountBefore);
        line.IrbpnrAmount.Should().Be(irbpnrAmountBefore);
    }
}
