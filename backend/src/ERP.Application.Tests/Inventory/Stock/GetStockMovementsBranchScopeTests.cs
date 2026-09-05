using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.Stock;

/// <summary>
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — GetStockMovementsQuery exige <c>WarehouseId</c> (a
/// diferencia de GetKardexByProductQuery/GetKardexByDocumentQuery/GetKardexMovementDetailQuery,
/// company-wide por diseño): esa bodega debe pertenecer a la sucursal activa antes de consultar
/// movimientos, mismo patrón que StockAdjustmentBranchOwnershipTests.
/// </summary>
public sealed class GetStockMovementsBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchAId = Guid.NewGuid();
    private static readonly Guid BranchBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

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

    private sealed class Fixture
    {
        public Mock<IStockRepository> StockRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public Fixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
        }

        public GetStockMovementsQueryHandler BuildHandler() =>
            new(StockRepo.Object, WarehouseRepo.Object, Tenant.Object, Branch.Object);
    }

    [Fact]
    public async Task Consultar_movimientos_de_bodega_de_otra_sucursal_es_rechazado()
    {
        var warehouseOfBranchB = CreateWarehouse(BranchBId);
        var f = new Fixture(activeBranchId: BranchAId);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchB);

        var result = await f.BuildHandler()
            .Handle(
                new GetStockMovementsQuery(ItemId, warehouseOfBranchB.Id, null, null),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        f.StockRepo.Verify(
            r =>
                r.GetMovementsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Consultar_movimientos_de_bodega_inexistente_es_rechazado()
    {
        var f = new Fixture(activeBranchId: BranchAId);
        var missingWarehouseId = Guid.NewGuid();
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, missingWarehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        var result = await f.BuildHandler()
            .Handle(
                new GetStockMovementsQuery(ItemId, missingWarehouseId, null, null),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Consultar_movimientos_de_bodega_de_la_sucursal_activa_funciona()
    {
        var warehouseOfBranchA = CreateWarehouse(BranchAId);
        var f = new Fixture(activeBranchId: BranchAId);
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouseOfBranchA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchA);
        f.StockRepo
            .Setup(r =>
                r.GetMovementsAsync(
                    TenantId,
                    ItemId,
                    warehouseOfBranchA.Id,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<StockMovement>());

        var result = await f.BuildHandler()
            .Handle(
                new GetStockMovementsQuery(ItemId, warehouseOfBranchA.Id, null, null),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
    }
}
