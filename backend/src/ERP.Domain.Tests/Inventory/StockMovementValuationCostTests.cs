using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

/// <summary>
/// TECH-DEBT-API-INVENTORY-ADJUSTMENT-FAILURE-01A — <see cref="StockMovement.UnitCost"/> debe
/// reflejar SOLO el costo capturado/manual del caller (no nulo únicamente en entradas);
/// <see cref="StockMovement.TotalCost"/> es el único campo que puede alimentarse del costo de
/// valuación resuelto internamente (<c>valuationUnitCost</c>, típicamente el costo promedio
/// corrido vigente) cuando no hubo costo manual — necesario para que Accounting pueda costear
/// salidas (COGS, ACCOUNTING-INVENTORY-COGS-07) sin que eso implique "esta salida capturó un
/// costo manual". Antes de este fix, <c>StockRepository.CreateAndTrackMovementAsync</c> pasaba el
/// costo de valuación resuelto como si fuera el <c>unitCost</c> crudo, contaminando
/// <see cref="StockMovement.UnitCost"/> en TODA salida (ventas, ajustes negativos, devoluciones de
/// compra) — ver InventoryAdjustmentsEndToEndTests.Escenario3 (ERP.API.Tests), que es la prueba de
/// integración que expuso el síntoma.
/// </summary>
public sealed class StockMovementValuationCostTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static StockMovement Create(decimal? unitCost, decimal? valuationUnitCost, decimal quantity = -2m) =>
        StockMovement.Create(
            TenantId,
            BranchId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            StockMovementType.NegativeAdjust,
            quantity,
            "UNIT",
            previousQuantity: 5m,
            sequenceNumber: 2,
            runningAverageCost: 6.181818m,
            runningStockValue: 24.727272m,
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reference: null,
            sourceDocId: null,
            sourceDocType: null,
            createdBy: UserId,
            companyId: CompanyId,
            unitCost: unitCost,
            valuationUnitCost: valuationUnitCost
        );

    // ── B1/B2/B3: egreso/ajuste negativo sin costo manual ───────────────────

    [Fact]
    public void Egreso_sin_unitCost_manual_deja_UnitCost_null_aunque_haya_valuationUnitCost()
    {
        var movement = Create(unitCost: null, valuationUnitCost: 6.181818m);

        movement.UnitCost.Should().BeNull("un egreso nunca captura un costo manual");
    }

    [Fact]
    public void Egreso_sin_unitCost_manual_calcula_TotalCost_desde_valuationUnitCost()
    {
        var movement = Create(unitCost: null, valuationUnitCost: 6.181818m, quantity: -2m);

        movement.TotalCost.Should().Be(Math.Abs(-2m) * 6.181818m);
    }

    [Fact]
    public void Egreso_sin_unitCost_ni_valuationUnitCost_deja_TotalCost_null()
    {
        var movement = Create(unitCost: null, valuationUnitCost: null);

        movement.UnitCost.Should().BeNull();
        movement.TotalCost.Should().BeNull();
    }

    // ── B4: entrada / ajuste positivo preserva costo manual ─────────────────

    [Fact]
    public void Ingreso_con_unitCost_manual_lo_persiste_en_UnitCost_ignorando_valuationUnitCost()
    {
        // valuationUnitCost distinto y deliberadamente "contaminante" — debe ser ignorado por
        // completo cuando el caller SÍ proveyó un costo manual explícito.
        var movement = Create(unitCost: 10m, valuationUnitCost: 999m, quantity: 5m);

        movement.UnitCost.Should().Be(10m);
    }

    [Fact]
    public void Ingreso_con_unitCost_manual_calcula_TotalCost_desde_unitCost_no_desde_valuationUnitCost()
    {
        var movement = Create(unitCost: 10m, valuationUnitCost: 999m, quantity: 5m);

        movement.TotalCost.Should().Be(5m * 10m);
    }
}
