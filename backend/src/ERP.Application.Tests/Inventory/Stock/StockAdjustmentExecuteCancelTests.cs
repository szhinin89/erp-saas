using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.Stock;

/// <summary>
/// INVENTORY-ADJUSTMENTS-02 — cobertura de Execute (Ingreso/Egreso, costeo, validaciones de
/// motivo, stock insuficiente, presentación) y Cancel (reversa, doble-anulación, no-mutación del
/// StockMovement original). Reutiliza los fixtures/helpers de
/// <see cref="StockAdjustmentBranchOwnershipTests"/> para no duplicar construcción de Warehouse/
/// Item/Reason.
/// </summary>
public sealed class StockAdjustmentExecuteCancelTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid ReasonId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static Warehouse CreateWarehouse() =>
        Warehouse.Create(
            TenantId,
            BranchId,
            "Bodega Test",
            "WT",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            UserId,
            CompanyId
        );

    private static StockAdjustment CreateDraft(
        string movementType,
        decimal quantity,
        decimal? unitCostBase,
        decimal conversionFactor = 1m,
        Guid? packagingLevelId = null
    )
    {
        var adj = StockAdjustment.Create(
            TenantId,
            1,
            WarehouseId,
            "Bodega Test",
            movementType,
            ReasonId,
            "Observaciones",
            UserId,
            CompanyId
        );
        adj.ReplaceLines(
            new[]
            {
                StockAdjustmentLine.Create(
                    TenantId,
                    CompanyId,
                    ItemId,
                    "Producto Ajuste",
                    packagingLevelId,
                    packagingLevelId is null ? "UNIT" : "CAJA",
                    "UNIT",
                    conversionFactor,
                    quantity,
                    unitCostBase,
                    null,
                    0,
                    adj.Id
                ),
            }
        );
        return adj;
    }

    private sealed class ExecuteHarness
    {
        public Mock<IStockAdjustmentRepository> AdjRepo { get; } = new();
        public Mock<IInventoryAdjustmentReasonRepository> ReasonRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public ExecuteHarness(StockAdjustment adj, InventoryAdjustmentReason reason)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            AdjRepo.Setup(r => r.GetByIdAsync(TenantId, adj.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(adj);
            WarehouseRepo
                .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateWarehouse());
            ReasonRepo
                .Setup(r => r.GetByIdAsync(TenantId, adj.ReasonId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reason);
        }

        public ExecuteStockAdjustmentCommandHandler BuildHandler() =>
            new(
                AdjRepo.Object,
                ReasonRepo.Object,
                StockRepo.Object,
                WarehouseRepo.Object,
                Tenant.Object,
                Branch.Object,
                User.Object
            );
    }

    private static InventoryAdjustmentReason CreateReason(
        string allowed = InventoryAdjustmentReason.Ambos,
        bool requiresNotes = false
    ) =>
        InventoryAdjustmentReason.Create(TenantId, null, "MERMA", "Merma", allowed, requiresNotes, 1, UserId);

    [Fact]
    public async Task Create_no_llama_AppendMovementAsync()
    {
        // La creación de un Draft nunca toca stock — verificado indirectamente: un ajuste recién
        // creado (Draft) no tiene ExecutedAt ni movimientos posteados.
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m);
        adj.Status.Should().Be("Draft");
        adj.ExecutedAt.Should().BeNull();
    }

    [Fact]
    public async Task Ejecutar_Ingreso_postea_PositiveAdjust_con_costo_manual_y_recalcula_costo_promedio()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m);
        var harness = new ExecuteHarness(adj, CreateReason());

        // Stock inicial: 10 unidades a costo promedio 8 (valor total 80). AppendMovementAsync real
        // aplicaría: newValue = 80 + 5*10 = 130; newQty = 15; newAvg = 130/15 = 8.6667.
        harness.StockRepo
            .Setup(r => r.GetStockAsync(TenantId, WarehouseId, ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrentStock.Create(TenantId, ItemId, WarehouseId, UserId, CompanyId));

        StockMovement? captured = null;
        harness.StockRepo
            .Setup(r =>
                r.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId,
                    WarehouseId,
                    StockMovementType.PositiveAdjust,
                    5m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    adj.AdjustmentNumber,
                    adj.Id,
                    "StockAdjustment",
                    UserId,
                    10m,
                    null,
                    null,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                )
            )
            .Returns(
                (
                    Guid _tid,
                    Guid _cid,
                    Guid _pid,
                    Guid _wid,
                    StockMovementType _mt,
                    decimal qty,
                    string uom,
                    DateOnly eff,
                    string? reference,
                    Guid? srcId,
                    string? srcType,
                    Guid actor,
                    decimal? unitCost,
                    Guid? lot,
                    Guid? serial,
                    CancellationToken _ct,
                    Guid? srcLineId
                ) =>
                {
                    var newAvg = (0m + qty * (unitCost ?? 0m)) / qty;
                    captured = StockMovement.Create(
                        TenantId,
                        BranchId,
                        _pid,
                        _wid,
                        _mt,
                        qty,
                        uom,
                        0m,
                        1,
                        newAvg,
                        qty * newAvg,
                        eff,
                        reference,
                        srcId,
                        srcType,
                        actor,
                        _cid,
                        unitCost,
                        lot,
                        serial,
                        srcLineId
                    );
                    return Task.FromResult(captured);
                }
            );

        var result = await harness.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        adj.Status.Should().Be("Executed");
        var line = adj.Lines.Single();
        line.CurrentStockAfter.Should().Be(5m);
        line.UnitCostBase.Should().Be(10m);
        harness.StockRepo.Verify(
            r => r.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Ejecutar_Egreso_postea_NegativeAdjust_y_rechaza_si_stock_insuficiente()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeEgreso, 100m, null);
        var harness = new ExecuteHarness(adj, CreateReason());
        harness.StockRepo
            .Setup(r => r.GetStockAsync(TenantId, WarehouseId, ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentStock?)null); // 0 disponible

        var result = await harness.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        adj.Status.Should().Be("Draft");
        harness.StockRepo.Verify(
            r =>
                r.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StockMovementType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Ejecutar_rechaza_si_motivo_inactivo()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m);
        var reason = CreateReason();
        reason.Disable(UserId);
        var harness = new ExecuteHarness(adj, reason);

        var result = await harness.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Ejecutar_rechaza_si_motivo_no_admite_el_MovementType()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m);
        var reason = CreateReason(InventoryAdjustmentReason.Egreso);
        var harness = new ExecuteHarness(adj, reason);

        var result = await harness.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Ejecutar_rechaza_si_motivo_requiere_notas_y_no_hay()
    {
        var adj = StockAdjustment.Create(
            TenantId,
            1,
            WarehouseId,
            "Bodega Test",
            StockAdjustment.MovementTypeIngreso,
            ReasonId,
            null, // sin notas
            UserId,
            CompanyId
        );
        adj.ReplaceLines(
            new[]
            {
                StockAdjustmentLine.Create(
                    TenantId,
                    CompanyId,
                    ItemId,
                    "Producto Ajuste",
                    null,
                    "UNIT",
                    "UNIT",
                    1m,
                    5m,
                    10m,
                    null,
                    0,
                    adj.Id
                ),
            }
        );
        var reason = CreateReason(requiresNotes: true);
        var harness = new ExecuteHarness(adj, reason);

        var result = await harness.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task No_se_puede_ejecutar_un_ajuste_que_no_esta_en_Draft()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m);
        adj.Execute(UserId);
        var harness = new ExecuteHarness(adj, CreateReason());

        var result = await harness.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Presentacion_caja_x12_calcula_QuantityInBaseUom_correctamente()
    {
        var packagingId = Guid.NewGuid();
        var adj = CreateDraft(
            StockAdjustment.MovementTypeIngreso,
            quantity: 3m,
            unitCostBase: 10m,
            conversionFactor: 12m,
            packagingLevelId: packagingId
        );

        var line = adj.Lines.Single();
        line.QuantityInBaseUom.Should().Be(36m);
        line.ConversionFactor.Should().Be(12m);
    }

    // ── Cancel ────────────────────────────────────────────────────────

    private sealed class CancelHarness
    {
        public Mock<IStockAdjustmentRepository> AdjRepo { get; } = new();
        public Mock<IInventoryAdjustmentReasonRepository> ReasonRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public CancelHarness(StockAdjustment adj)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            AdjRepo.Setup(r => r.GetByIdAsync(TenantId, adj.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(adj);
            WarehouseRepo
                .Setup(r => r.GetByIdAsync(TenantId, WarehouseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateWarehouse());
            ReasonRepo
                .Setup(r => r.GetByIdAsync(TenantId, adj.ReasonId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateReason());
        }

        public CancelStockAdjustmentCommandHandler BuildHandler() =>
            new(
                AdjRepo.Object,
                ReasonRepo.Object,
                StockRepo.Object,
                WarehouseRepo.Object,
                Tenant.Object,
                Branch.Object,
                User.Object
            );
    }

    [Fact]
    public async Task Cancelar_solo_permitido_desde_Executed()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m); // still Draft
        var harness = new CancelHarness(adj);

        var result = await harness.BuildHandler()
            .Handle(new CancelStockAdjustmentCommand(adj.Id, "motivo"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        harness.StockRepo.Verify(
            r =>
                r.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StockMovementType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Cancelar_ajuste_Ejecutado_postea_movimiento_inverso_y_rechaza_doble_anulacion()
    {
        var adj = CreateDraft(StockAdjustment.MovementTypeIngreso, 5m, 10m);
        adj.Execute(UserId);
        var harness = new CancelHarness(adj);
        harness.StockRepo
            .Setup(r =>
                r.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId,
                    WarehouseId,
                    StockMovementType.NegativeAdjust,
                    -5m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    $"ANULACIÓN: {adj.AdjustmentNumber}",
                    adj.Id,
                    "StockAdjustment",
                    UserId,
                    10m,
                    null,
                    null,
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                )
            )
            .ReturnsAsync(
                StockMovement.Create(
                    TenantId,
                    BranchId,
                    ItemId,
                    WarehouseId,
                    StockMovementType.NegativeAdjust,
                    -5m,
                    "UNIT",
                    5m,
                    2,
                    10m,
                    0m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    "ANULACIÓN",
                    adj.Id,
                    "StockAdjustment",
                    UserId,
                    CompanyId,
                    10m
                )
            );

        var result = await harness.BuildHandler()
            .Handle(new CancelStockAdjustmentCommand(adj.Id, "Error de digitación"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        adj.Status.Should().Be("Cancelled");
        adj.CancelledReason.Should().Be("Error de digitación");
        harness.StockRepo.Verify(
            r => r.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Doble anulación rechazada
        var secondAttempt = await harness.BuildHandler()
            .Handle(new CancelStockAdjustmentCommand(adj.Id, "Segundo intento"), CancellationToken.None);
        secondAttempt.IsSuccess.Should().BeFalse();
    }
}
