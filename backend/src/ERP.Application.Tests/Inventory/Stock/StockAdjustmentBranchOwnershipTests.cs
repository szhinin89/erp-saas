using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.Stock;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-5) — CreateStockAdjustmentCommandHandler nunca validaba que
/// <c>WarehouseId</c> (recibido del cliente) exista y pertenezca a la sucursal activa;
/// ExecuteStockAdjustmentCommandHandler tampoco resolvía la bodega del ajuste antes de postear el
/// movimiento de Kardex real. Ambos permitían operar sobre la bodega de otra sucursal de la misma
/// empresa por GUID.
/// INVENTORY-ADJUSTMENTS-02 — actualizado para el nuevo shape (header + líneas + Reason
/// administrable) y extendido a Cancel (mismo gap de Branch Ownership).
/// </summary>
public sealed class StockAdjustmentBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchAId = Guid.NewGuid();
    private static readonly Guid BranchBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid ReasonId = Guid.NewGuid();

    private static Warehouse CreateWarehouse(Guid branchId) =>
        Warehouse.Create(
            TenantId,
            branchId,
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

    internal static Item CreateItem() =>
        Item.Create(
            TenantId,
            "SKU-ADJ",
            "Producto Ajuste",
            "Producto Ajuste",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );

    internal static InventoryAdjustmentReason CreateReason(
        string allowedMovementType = InventoryAdjustmentReason.Ambos,
        bool requiresNotes = false
    ) =>
        InventoryAdjustmentReason.Create(
            TenantId,
            null,
            "MERMA",
            "Merma",
            allowedMovementType,
            requiresNotes,
            1,
            UserId
        );

    // ── CreateStockAdjustmentCommandHandler ──────────────────────────────

    private sealed class CreateFixture
    {
        public Mock<IStockAdjustmentRepository> AdjRepo { get; } = new();
        public Mock<IInventoryAdjustmentReasonRepository> ReasonRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public CreateFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            var item = CreateItem();
            ItemRepo
                .Setup(r => r.GetByIdAsync(ItemId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

            ReasonRepo
                .Setup(r => r.GetByIdAsync(TenantId, ReasonId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateReason());
        }

        public CreateStockAdjustmentCommandHandler BuildHandler() =>
            new(
                AdjRepo.Object,
                ReasonRepo.Object,
                WarehouseRepo.Object,
                ItemRepo.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object
            );
    }

    private static CreateStockAdjustmentCommand BuildCreateCommand(Guid warehouseId) =>
        new(
            warehouseId,
            "Bodega Test",
            StockAdjustment.MovementTypeIngreso,
            ReasonId,
            null,
            new[]
            {
                new CreateStockAdjustmentLineInput(ItemId, "Producto Ajuste", null, 5m, 10m, null),
            }
        );

    [Fact]
    public async Task Crear_ajuste_con_bodega_de_otra_sucursal_es_rechazado()
    {
        var warehouseOfBranchB = CreateWarehouse(BranchBId);
        var f = new CreateFixture(activeBranchId: BranchAId);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchB);

        var result = await f.BuildHandler()
            .Handle(BuildCreateCommand(warehouseOfBranchB.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.AdjRepo.Verify(r => r.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_ajuste_con_bodega_inexistente_es_rechazado()
    {
        var f = new CreateFixture(activeBranchId: BranchAId);
        var missingWarehouseId = Guid.NewGuid();
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, missingWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        var result = await f.BuildHandler()
            .Handle(BuildCreateCommand(missingWarehouseId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.AdjRepo.Verify(r => r.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_ajuste_con_bodega_de_la_sucursal_activa_funciona()
    {
        var warehouseOfBranchA = CreateWarehouse(BranchAId);
        var f = new CreateFixture(activeBranchId: BranchAId);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchA);
        f.AdjRepo.Setup(r => r.GetNextSequentialAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await f.BuildHandler()
            .Handle(BuildCreateCommand(warehouseOfBranchA.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.AdjRepo.Verify(r => r.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ExecuteStockAdjustmentCommandHandler ─────────────────────────────

    internal sealed class ExecuteFixture
    {
        public Mock<IStockAdjustmentRepository> AdjRepo { get; } = new();
        public Mock<IInventoryAdjustmentReasonRepository> ReasonRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public ExecuteFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            ReasonRepo
                .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateReason());
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

    internal static StockAdjustment CreateDraftAdjustmentWithLine(
        Guid warehouseId,
        string movementType = StockAdjustment.MovementTypeIngreso,
        decimal quantity = 5m,
        decimal? unitCostBase = 10m
    )
    {
        var adj = StockAdjustment.Create(
            TenantId,
            1,
            warehouseId,
            "Bodega Test",
            movementType,
            ReasonId,
            null,
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

    [Fact]
    public async Task Ejecutar_ajuste_de_bodega_de_otra_sucursal_devuelve_NotFound()
    {
        var warehouseOfBranchB = CreateWarehouse(BranchBId);
        var adj = CreateDraftAdjustmentWithLine(warehouseOfBranchB.Id);
        var f = new ExecuteFixture(activeBranchId: BranchAId);
        f.AdjRepo.Setup(r => r.GetByIdAsync(TenantId, adj.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adj);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchB);

        var result = await f.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.StockRepo.Verify(
            r =>
                r.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ERP.Domain.Modules.Inventory.Enums.StockMovementType>(),
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
    public async Task Ejecutar_ajuste_de_la_sucursal_activa_funciona()
    {
        var warehouseOfBranchA = CreateWarehouse(BranchAId);
        var adj = CreateDraftAdjustmentWithLine(warehouseOfBranchA.Id);
        var f = new ExecuteFixture(activeBranchId: BranchAId);
        f.AdjRepo.Setup(r => r.GetByIdAsync(TenantId, adj.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adj);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchA);
        f.StockRepo
            .Setup(r =>
                r.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId,
                    warehouseOfBranchA.Id,
                    ERP.Domain.Modules.Inventory.Enums.StockMovementType.PositiveAdjust,
                    5m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
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
                    BranchAId,
                    ItemId,
                    warehouseOfBranchA.Id,
                    ERP.Domain.Modules.Inventory.Enums.StockMovementType.PositiveAdjust,
                    5m,
                    "UNIT",
                    0m,
                    1,
                    10m,
                    50m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    adj.AdjustmentNumber,
                    adj.Id,
                    "StockAdjustment",
                    UserId,
                    CompanyId,
                    10m
                )
            );

        var result = await f.BuildHandler()
            .Handle(new ExecuteStockAdjustmentCommand(adj.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        adj.Status.Should().Be("Executed");
    }

    // ── CancelStockAdjustmentCommandHandler ──────────────────────────────

    private sealed class CancelFixture
    {
        public Mock<IStockAdjustmentRepository> AdjRepo { get; } = new();
        public Mock<IInventoryAdjustmentReasonRepository> ReasonRepo { get; } = new();
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public CancelFixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            ReasonRepo
                .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
    public async Task Anular_ajuste_de_bodega_de_otra_sucursal_devuelve_NotFound()
    {
        var warehouseOfBranchB = CreateWarehouse(BranchBId);
        var adj = CreateDraftAdjustmentWithLine(warehouseOfBranchB.Id);
        adj.Execute(UserId);
        var f = new CancelFixture(activeBranchId: BranchAId);
        f.AdjRepo.Setup(r => r.GetByIdAsync(TenantId, adj.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adj);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchB);

        var result = await f.BuildHandler()
            .Handle(new CancelStockAdjustmentCommand(adj.Id, "Error de digitación"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.StockRepo.Verify(
            r =>
                r.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<ERP.Domain.Modules.Inventory.Enums.StockMovementType>(),
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
}
