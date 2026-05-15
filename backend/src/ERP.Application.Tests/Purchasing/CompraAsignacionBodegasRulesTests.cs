using FluentAssertions;
using ERP.Application.Modules.Purchasing;
using ERP.Application.Modules.Purchasing.UseCases.CrearCompra;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Moq;

namespace ERP.Application.Tests.Compras;

public sealed class CompraAsignacionBodegasRulesTests
{
    [Fact]
    public async Task ValidateAsync_returns_error_when_sum_mismatch()
    {
        var b1 = Guid.NewGuid();
        var detalles = new[]
        {
            new DetalleCompraInput("A", null, Guid.NewGuid(), 10m, 1m, 0m, 0m),
        };
        var asignaciones = new[]
        {
            new AsignacionBodegaRequest(0, b1, 6m),
        };

        var bodegas = new Mock<IWarehouseRepository>();
        bodegas.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), b1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Warehouse.Create(Guid.NewGuid(), Guid.NewGuid(), "B1", null, null, Guid.NewGuid()));

        var err = await CompraAsignacionBodegasRules.ValidateAsync(
            detalles, asignaciones, Guid.NewGuid(), bodegas.Object, CancellationToken.None);

        err.Should().NotBeNull();
        err.Should().Contain("10");
    }

    [Fact]
    public async Task ValidateAsync_succeeds_when_two_lines_split_across_two_bodegas()
    {
        var tenantId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();

        var detalles = new[]
        {
            new DetalleCompraInput("P1", null, p1, 10m, 1m, 0m, 0m),
            new DetalleCompraInput("P2", null, p2, 5m, 1m, 0m, 0m),
        };
        var asignaciones = new[]
        {
            new AsignacionBodegaRequest(0, b1, 6m),
            new AsignacionBodegaRequest(0, b2, 4m),
            new AsignacionBodegaRequest(1, b2, 5m),
        };

        var bodegas = new Mock<IWarehouseRepository>();
        bodegas.Setup(x => x.GetByIdAsync(tenantId, b1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Warehouse.Create(tenantId, Guid.NewGuid(), "B1", null, null, Guid.NewGuid()));
        bodegas.Setup(x => x.GetByIdAsync(tenantId, b2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Warehouse.Create(tenantId, Guid.NewGuid(), "B2", null, null, Guid.NewGuid()));

        var err = await CompraAsignacionBodegasRules.ValidateAsync(
            detalles, asignaciones, tenantId, bodegas.Object, CancellationToken.None);

        err.Should().BeNull();
    }
}

