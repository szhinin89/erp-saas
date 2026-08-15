using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — PurchaseInvoice.DistributeAdditionalCost: prorrateo
/// aditivo de un monto nuevo (flete/otro gasto) únicamente entre las líneas incluidas por el
/// usuario, sumando a FreightAllocated/OtherCostsAllocated sin tocar las líneas excluidas.
/// </summary>
public sealed class PurchaseInvoiceDistributeAdditionalCostTests
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

    [Fact]
    public void Distribuye_proporcionalmente_por_subtotal_base_y_suma_a_FreightAllocated()
    {
        // Línea A: 100, Línea B: 300 → base total 400. Flete 40 → A=10, B=30 (última absorbe residuo).
        var inv = CreateDraftWithLines((1m, 100m), (1m, 300m));
        var lineA = inv.Lines[0];
        var lineB = inv.Lines[1];

        inv.DistributeAdditionalCost(
            PurchaseCostType.Freight,
            40m,
            new[] { lineA.Id, lineB.Id },
            UserId
        );

        lineA.FreightAllocated.Should().Be(10m);
        lineB.FreightAllocated.Should().Be(30m);
        (lineA.FreightAllocated + lineB.FreightAllocated).Should().Be(40m);
    }

    [Fact]
    public void Es_aditivo_no_reemplaza_FreightAllocated_previamente_asignado()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m));
        var lineA = inv.Lines[0];
        var lineB = inv.Lines[1];

        inv.DistributeAdditionalCost(PurchaseCostType.Freight, 10m, new[] { lineA.Id, lineB.Id }, UserId);
        inv.DistributeAdditionalCost(PurchaseCostType.Freight, 20m, new[] { lineA.Id, lineB.Id }, UserId);

        // Cada aplicación reparte 50/50 (bases iguales): +5 luego +10 = 15 por línea.
        lineA.FreightAllocated.Should().Be(15m);
        lineB.FreightAllocated.Should().Be(15m);
    }

    [Fact]
    public void No_toca_lineas_excluidas()
    {
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m), (1m, 100m));
        var included = new[] { inv.Lines[0].Id, inv.Lines[1].Id };
        var excludedLine = inv.Lines[2];

        inv.DistributeAdditionalCost(PurchaseCostType.OtherCost, 20m, included, UserId);

        excludedLine.OtherCostsAllocated.Should().Be(0m);
        inv.Lines[0].OtherCostsAllocated.Should().Be(10m);
        inv.Lines[1].OtherCostsAllocated.Should().Be(10m);
    }

    [Fact]
    public void Actualiza_LandedUnitCost_de_las_lineas_incluidas()
    {
        var inv = CreateDraftWithLines((10m, 10m)); // subtotal 100, qty base 10
        var line = inv.Lines[0];

        inv.DistributeAdditionalCost(PurchaseCostType.Freight, 20m, new[] { line.Id }, UserId);

        // TotalLineCost = 100 (taxable) + 20 (freight) = 120; LandedUnitCost = 120/10 = 12
        line.LandedUnitCost.Should().Be(12m);
    }

    [Fact]
    public void Ultima_linea_incluida_absorbe_el_residuo_de_redondeo()
    {
        // 3 líneas con base igual (100 c/u) y monto que no divide exacto entre 3 → 3.33/3.33/3.34
        var inv = CreateDraftWithLines((1m, 100m), (1m, 100m), (1m, 100m));
        var ids = inv.Lines.Select(l => l.Id).ToArray();

        inv.DistributeAdditionalCost(PurchaseCostType.Freight, 10m, ids, UserId);

        var total = inv.Lines.Sum(l => l.FreightAllocated);
        total.Should().Be(10m);
        inv.Lines[2].FreightAllocated.Should().Be(10m - inv.Lines[0].FreightAllocated - inv.Lines[1].FreightAllocated);
    }

    [Fact]
    public void Monto_cero_o_negativo_lanza_ArgumentException()
    {
        var inv = CreateDraftWithLines((1m, 100m));
        var ids = new[] { inv.Lines[0].Id };

        var act = () => inv.DistributeAdditionalCost(PurchaseCostType.Freight, 0m, ids, UserId);

        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Sin_lineas_incluidas_lanza_ArgumentException()
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

    [Fact]
    public void Linea_incluida_que_no_pertenece_a_la_compra_lanza_ArgumentException()
    {
        var inv = CreateDraftWithLines((1m, 100m));

        var act = () => inv.DistributeAdditionalCost(
            PurchaseCostType.Freight,
            10m,
            new[] { Guid.NewGuid() },
            UserId
        );

        act.Should().Throw<ArgumentException>().WithParameterName("includedLineIds");
    }

    [Fact]
    public void Base_imponible_total_incluida_en_cero_reparte_equitativamente_sin_lanzar()
    {
        // PURCHASE-LANDED-COST-PRORATION-CONSISTENCY-01 — antes lanzaba InvalidOperationException;
        // ahora, igual que ProrateCostToLines, cae a reparto equitativo entre las líneas incluidas
        // en vez de bloquear la operación, siempre que exista al menos una línea elegible.
        var inv = CreateDraftWithLines((1m, 0m), (1m, 0m), (1m, 0m));
        var ids = inv.Lines.Select(l => l.Id).ToArray();

        var act = () => inv.DistributeAdditionalCost(PurchaseCostType.Freight, 10m, ids, UserId);

        act.Should().NotThrow();
        var total = inv.Lines.Sum(l => l.FreightAllocated);
        total.Should().Be(10m);
        inv.Lines[0].FreightAllocated.Should().Be(inv.Lines[1].FreightAllocated);
    }
}
