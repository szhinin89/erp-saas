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
    public async Task PurchBillApprovedEventHandler_crea_stock_y_movimientos()
    {
        var subscriberId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var provId   = Guid.NewGuid();
        var p1       = Guid.NewGuid();
        var p2       = Guid.NewGuid();
        var b1       = Guid.NewGuid();
        var b2       = Guid.NewGuid();

        var lines = new[]
        {
            new PurchBillApprovedStockLine(Guid.NewGuid(), p1, b1, 6m, 1m),
            new PurchBillApprovedStockLine(Guid.NewGuid(), p1, b2, 4m, 1m),
            new PurchBillApprovedStockLine(Guid.NewGuid(), p2, b2, 5m, 1m),
        };

        var movimientos = new List<StockMovement>();
        var stocks      = new List<CurrentStock>();

        var inv = new Mock<IStockRepository>();
        inv.Setup(x => x.GetStockAsync(subscriberId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentStock?)null);
        inv.Setup(x => x.AddCurrentStockAsync(It.IsAny<CurrentStock>(), It.IsAny<CancellationToken>()))
            .Callback<CurrentStock, CancellationToken>((s, _) => stocks.Add(s))
            .Returns(Task.CompletedTask);
        inv.Setup(x => x.AddMovementAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
            .Callback<StockMovement, CancellationToken>((m, _) => movimientos.Add(m))
            .Returns(Task.CompletedTask);

        var handler = new PurchBillApprovedEventHandler(
            inv.Object,
            NullLogger<PurchBillApprovedEventHandler>.Instance);

        var ev = new PurchBillApprovedEvent(Guid.NewGuid(), subscriberId, "F-9001", userId, lines);
        await handler.Handle(ev, CancellationToken.None);

        movimientos.Should().HaveCount(3);
        movimientos.Sum(m => m.Quantity).Should().Be(15m);
        stocks.Should().HaveCount(3);
    }

    [Fact]
    public async Task AprobarCompra_agrega_CompraAprobadaEvent_y_no_llama_inventario_directamente()
    {
        var subscriberId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var provId   = Guid.NewGuid();
        var p1       = Guid.NewGuid();
        var p2       = Guid.NewGuid();
        var b1       = Guid.NewGuid();
        var b2       = Guid.NewGuid();

        var compra = PurchBill.Create(
            subscriberId, provId, "F-9001", null, null,
            DateTime.UtcNow.Date, null, "Contado", null, userId);
        compra.AddLine("P1", null, p1, 10m, 1m, 0m, 0m, userId);
        compra.AddLine("P2", null, p2, 5m, 1m, 0m, 0m, userId);
        compra.Validate(userId);

        var d0 = compra.Lines[0].Id;
        var d1 = compra.Lines[1].Id;

        IReadOnlyList<PurchWarehouseAlloc> asignaciones =
        [
            PurchWarehouseAlloc.Create(subscriberId, compra.Id, d0, b1, p1, 6m, userId),
            PurchWarehouseAlloc.Create(subscriberId, compra.Id, d0, b2, p1, 4m, userId),
            PurchWarehouseAlloc.Create(subscriberId, compra.Id, d1, b2, p2, 5m, userId),
        ];

        var repo = new Mock<IPurchBillRepository>();
        repo.Setup(x => x.GetByIdAsync(subscriberId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compra);
        repo.Setup(x => x.GetWarehouseAllocsByBillIdAsync(subscriberId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asignaciones);

        var accounting = new Mock<ERP.Application.Common.Interfaces.IAccountingService>();
        var asientoId = Guid.NewGuid();
        accounting.Setup(x => x.CrearAsientoCompraAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(asientoId));

        var inv = new Mock<IStockRepository>();
        var activity = new Mock<IUserActivityRepository>();
        activity.Setup(x => x.AddAsync(It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentSubscriber>();
        tenant.SetupGet(x => x.SubscriberId).Returns(subscriberId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);
        user.SetupGet(x => x.Email).Returns("t@test");
        user.SetupGet(x => x.FullName).Returns("Test");

        var handler = new ApprovePurchaseCommandHandler(
            repo.Object,
            accounting.Object,
            activity.Object,
            tenant.Object,
            user.Object,
            uow.Object,
            NullLogger<ApprovePurchaseCommandHandler>.Instance);

        var result = await handler.Handle(new ApprovePurchaseCommand(compra.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        compra.Status.Should().Be(PurchaseStatus.Approved);
        inv.Verify(x => x.AddMovementAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()), Times.Never);
        inv.Verify(x => x.GetStockAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        compra.DomainEvents.Should().ContainSingle(e => e is PurchBillApprovedEvent);
        var ev = (PurchBillApprovedEvent)compra.DomainEvents.Single(e => e is PurchBillApprovedEvent);
        ev.StockLines.Should().HaveCount(3);

        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Aprobar_rolls_back_when_accounting_fails()
    {
        var subscriberId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var provId   = Guid.NewGuid();

        var compra = PurchBill.Create(
            subscriberId, provId, "F-9002", null, null,
            DateTime.UtcNow.Date, null, "Contado", null, userId);
        compra.AddLine("X", null, Guid.NewGuid(), 1m, 1m, 0m, 0m, userId);
        compra.Validate(userId);

        var repo = new Mock<IPurchBillRepository>();
        repo.Setup(x => x.GetByIdAsync(subscriberId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(compra);
        repo.Setup(x => x.GetWarehouseAllocsByBillIdAsync(subscriberId, compra.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PurchWarehouseAlloc>());

        var accounting = new Mock<ERP.Application.Common.Interfaces.IAccountingService>();
        accounting.Setup(x => x.CrearAsientoCompraAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("sin cuentas"));

        var inv = new Mock<IStockRepository>();
        var activity = new Mock<IUserActivityRepository>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentSubscriber>();
        tenant.SetupGet(x => x.SubscriberId).Returns(subscriberId);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(userId);
        user.SetupGet(x => x.Email).Returns("t@test");
        user.SetupGet(x => x.FullName).Returns("Test");

        var handler = new ApprovePurchaseCommandHandler(
            repo.Object,
            accounting.Object,
            activity.Object,
            tenant.Object,
            user.Object,
            uow.Object,
            NullLogger<ApprovePurchaseCommandHandler>.Instance);

        var result = await handler.Handle(new ApprovePurchaseCommand(compra.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        compra.Status.Should().Be(PurchaseStatus.Validated);
        uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}


