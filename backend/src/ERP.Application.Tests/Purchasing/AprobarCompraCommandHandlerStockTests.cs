using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;
using ERP.Application.Modules.Inventory.EventHandlers;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Compras;

public sealed class AprobarCompraCommandHandlerStockTests
{
    [Fact]
    public async Task CompraAprobadaEventHandler_crea_stock_y_movimientos()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var provId   = Guid.NewGuid();
        var p1       = Guid.NewGuid();
        var p2       = Guid.NewGuid();
        var b1       = Guid.NewGuid();
        var b2       = Guid.NewGuid();

        var lines = new[]
        {
            new CompraAprobadaStockLine(Guid.NewGuid(), p1, b1, 6m, 1m),
            new CompraAprobadaStockLine(Guid.NewGuid(), p1, b2, 4m, 1m),
            new CompraAprobadaStockLine(Guid.NewGuid(), p2, b2, 5m, 1m),
        };

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

        var handler = new CompraAprobadaEventHandler(
            inv.Object,
            NullLogger<CompraAprobadaEventHandler>.Instance);

        var ev = new CompraAprobadaEvent(Guid.NewGuid(), tenantId, "F-9001", userId, lines);
        await handler.Handle(ev, CancellationToken.None);

        movimientos.Should().HaveCount(3);
        movimientos.Sum(m => m.Cantidad).Should().Be(15m);
        stocks.Should().HaveCount(3);
    }

    [Fact]
    public async Task AprobarCompra_agrega_CompraAprobadaEvent_y_no_llama_inventario_directamente()
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

        var inv = new Mock<IInventarioStockRepository>();
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
            activity.Object,
            tenant.Object,
            user.Object,
            uow.Object,
            NullLogger<AprobarCompraCommandHandler>.Instance);

        var result = await handler.Handle(new AprobarCompraCommand(compra.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        compra.Estado.Should().Be(EstadoCompra.Aprobado);
        inv.Verify(x => x.AddMovimientoAsync(It.IsAny<InventarioMovimiento>(), It.IsAny<CancellationToken>()), Times.Never);
        inv.Verify(x => x.GetStockByTenantBodegaProductAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        compra.DomainEvents.Should().ContainSingle(e => e is CompraAprobadaEvent);
        var ev = (CompraAprobadaEvent)compra.DomainEvents.Single(e => e is CompraAprobadaEvent);
        ev.StockLines.Should().HaveCount(3);

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
