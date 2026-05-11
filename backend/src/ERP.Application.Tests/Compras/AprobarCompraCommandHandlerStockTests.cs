using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.UseCases.AprobarCompra;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Compras.Entities;
using ERP.Domain.Modules.Compras.Enums;
using ERP.Domain.Modules.Compras.Interfaces;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Compras;

public sealed class AprobarCompraCommandHandlerStockTests
{
    [Fact]
    public async Task Aprobar_creates_stock_and_movements_for_each_assignment_with_producto()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var provId   = Guid.NewGuid();
        var p1       = Guid.NewGuid();
        var p2       = Guid.NewGuid();
        var b1       = Guid.NewGuid();
        var b2       = Guid.NewGuid();

        var compra = CompraFactura.Create(
            tenantId, provId, "F-9001", null, null,
            DateTime.UtcNow.Date, null, "Contado", null, userId);
        compra.AgregarDetalle("P1", null, p1, 10m, 1m, 0m, 0m, userId);
        compra.AgregarDetalle("P2", null, p2, 5m, 1m, 0m, 0m, userId);
        compra.Validar(userId);

        var d0 = compra.Detalles[0].Id;
        var d1 = compra.Detalles[1].Id;

        IReadOnlyList<CompraBodegaAsignacion> asignaciones =
        [
            CompraBodegaAsignacion.Create(tenantId, compra.Id, d0, b1, p1, 6m, userId),
            CompraBodegaAsignacion.Create(tenantId, compra.Id, d0, b2, p1, 4m, userId),
            CompraBodegaAsignacion.Create(tenantId, compra.Id, d1, b2, p2, 5m, userId),
        ];

        var repo = new Mock<ICompraRepository>();
        repo.Setup(x => x.GetByIdWithDetailsAsync(tenantId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compra);
        repo.Setup(x => x.GetBodegaAsignacionesByCompraFacturaIdAsync(tenantId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asignaciones);

        var accounting = new Mock<ERP.Application.Common.Interfaces.IAccountingService>();
        var asientoId = Guid.NewGuid();
        accounting.Setup(x => x.CrearAsientoCompraAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(asientoId));

        var movimientos = new List<InventarioMovimiento>();
        var stocks      = new List<StockActual>();

        var inv = new Mock<IInventarioStockRepository>();
        inv.Setup(x => x.GetStockByTenantBodegaProductAsync(tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockActual?)null);
        inv.Setup(x => x.AddStockActualAsync(It.IsAny<StockActual>(), It.IsAny<CancellationToken>()))
            .Callback<StockActual, CancellationToken>((s, _) => stocks.Add(s))
            .Returns(Task.CompletedTask);
        inv.Setup(x => x.AddMovimientoAsync(It.IsAny<InventarioMovimiento>(), It.IsAny<CancellationToken>()))
            .Callback<InventarioMovimiento, CancellationToken>((m, _) => movimientos.Add(m))
            .Returns(Task.CompletedTask);

        var activity = new Mock<IUserActivityRepository>();
        activity.Setup(x => x.AddAsync(It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);
        user.SetupGet(x => x.Email).Returns("t@test");
        user.SetupGet(x => x.FullName).Returns("Test");

        var handler = new AprobarCompraCommandHandler(
            repo.Object,
            accounting.Object,
            inv.Object,
            activity.Object,
            tenant.Object,
            user.Object,
            uow.Object,
            NullLogger<AprobarCompraCommandHandler>.Instance);

        var result = await handler.Handle(new AprobarCompraCommand(compra.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        compra.Estado.Should().Be(EstadoCompra.Aprobado);
        movimientos.Should().HaveCount(3);
        movimientos.Sum(m => m.Cantidad).Should().Be(15m);
        stocks.Should().HaveCount(3);

        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Aprobar_rolls_back_when_accounting_fails()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var provId   = Guid.NewGuid();

        var compra = CompraFactura.Create(
            tenantId, provId, "F-9002", null, null,
            DateTime.UtcNow.Date, null, "Contado", null, userId);
        compra.AgregarDetalle("X", null, Guid.NewGuid(), 1m, 1m, 0m, 0m, userId);
        compra.Validar(userId);

        var repo = new Mock<ICompraRepository>();
        repo.Setup(x => x.GetByIdWithDetailsAsync(tenantId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compra);
        repo.Setup(x => x.GetBodegaAsignacionesByCompraFacturaIdAsync(tenantId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompraBodegaAsignacion>());

        var accounting = new Mock<ERP.Application.Common.Interfaces.IAccountingService>();
        accounting.Setup(x => x.CrearAsientoCompraAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("sin cuentas"));

        var inv = new Mock<IInventarioStockRepository>();
        var activity = new Mock<IUserActivityRepository>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);
        user.SetupGet(x => x.Email).Returns("t@test");
        user.SetupGet(x => x.FullName).Returns("Test");

        var handler = new AprobarCompraCommandHandler(
            repo.Object,
            accounting.Object,
            inv.Object,
            activity.Object,
            tenant.Object,
            user.Object,
            uow.Object,
            NullLogger<AprobarCompraCommandHandler>.Instance);

        var result = await handler.Handle(new AprobarCompraCommand(compra.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        compra.Estado.Should().Be(EstadoCompra.Validado);
        uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
