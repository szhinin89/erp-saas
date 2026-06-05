using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchase;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Moq;

namespace ERP.Application.Tests.Compras;

public sealed class PurchaseWarehouseAllocationRulesTests
{
    [Fact]
    public async Task ValidateAsync_returns_error_when_sum_mismatch()
    {
        var b1 = Guid.NewGuid();
        var lines = new[]
        {
            new PurchaseLineInput("A", null, Guid.NewGuid(), 10m, 1m, 0m, 0m),
        };
        var asignaciones = new[]
        {
            new WarehouseAllocationRequest(0, b1, 6m),
        };

        var bodegas = new Mock<IWarehouseRepository>();
        bodegas.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), b1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Warehouse.Create(Guid.NewGuid(), Guid.NewGuid(), "B1", "B1", null, null, null, null, null, null, null, null, null, Guid.NewGuid(), Guid.NewGuid()));

        var err = await PurchaseAsignacionWarehousesRules.ValidateAsync(
            lines, asignaciones, Guid.NewGuid(), bodegas.Object, CancellationToken.None);

        err.Should().NotBeNull();
        err.Should().Contain("10");
    }

    [Fact]
    public async Task ValidateAsync_succeeds_when_two_lines_split_across_two_bodegas()
    {
        var subscriberId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();

        var lines = new[]
        {
            new PurchaseLineInput("P1", null, p1, 10m, 1m, 0m, 0m),
            new PurchaseLineInput("P2", null, p2, 5m, 1m, 0m, 0m),
        };
        var asignaciones = new[]
        {
            new WarehouseAllocationRequest(0, b1, 6m),
            new WarehouseAllocationRequest(0, b2, 4m),
            new WarehouseAllocationRequest(1, b2, 5m),
        };

        var bodegas = new Mock<IWarehouseRepository>();
        bodegas.Setup(x => x.GetByIdAsync(subscriberId, b1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Warehouse.Create(subscriberId, Guid.NewGuid(), "B1", "B1", null, null, null, null, null, null, null, null, null, Guid.NewGuid(), Guid.NewGuid()));
        bodegas.Setup(x => x.GetByIdAsync(subscriberId, b2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Warehouse.Create(subscriberId, Guid.NewGuid(), "B2", "B2", null, null, null, null, null, null, null, null, null, Guid.NewGuid(), Guid.NewGuid()));

        var err = await PurchaseAsignacionWarehousesRules.ValidateAsync(
            lines, asignaciones, subscriberId, bodegas.Object, CancellationToken.None);

        err.Should().BeNull();
    }
}

