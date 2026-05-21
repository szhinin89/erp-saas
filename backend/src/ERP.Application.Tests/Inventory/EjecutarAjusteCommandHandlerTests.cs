using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory.DTOs;
using ERP.Application.Inventory.UseCases.EjecutarAjuste;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Inventario;

/// <summary>
/// Pruebas unitarias de EjecutarAjusteCommandHandler con mocks de IStockRepository.
/// Verifican el ajuste atómico de stock para los casos de incremento y disminución.
/// </summary>
public sealed class EjecutarAjusteCommandHandlerTests
{
    // ── Incremento (cantidad positiva) ────────────────────────────────────

    [Fact]
    public async Task Ejecutar_incremento_llama_IncrementarAtomico_y_crea_movimiento_AjustePositivo()
    {
        var ctx = new TestContext(cantidadAjuste: +15m);
        ctx.WithIncrementoExitoso(cantAnterior: 50m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Executed");
        result.Value.ExecutionDate.Should().NotBeNull();

        ctx.Movimientos.Should().HaveCount(1);
        ctx.Movimientos[0].MovementType.Should().Be(StockMovementType.PositiveAdjust);
        ctx.Movimientos[0].Quantity.Should().Be(15m);
        ctx.Movimientos[0].PreviousQuantity.Should().Be(50m);
        ctx.Movimientos[0].SourceDocType.Should().Be("StockAdjustment");

        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ejecutar_incremento_no_llama_DecrementarAtomico()
    {
        var ctx = new TestContext(cantidadAjuste: +5m);
        ctx.WithIncrementoExitoso(cantAnterior: 0m);

        await ctx.Handle();

        ctx.StockRepo.Verify(x => x.DecrementStockAtomicAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Disminución (cantidad negativa) ──────────────────────────────────

    [Fact]
    public async Task Ejecutar_disminucion_llama_DecrementarAtomico_y_crea_movimiento_AjusteNegativo()
    {
        var ctx = new TestContext(cantidadAjuste: -10m);
        ctx.WithDecrementoExitoso(cantAnterior: 30m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        ctx.Movimientos.Should().HaveCount(1);
        ctx.Movimientos[0].MovementType.Should().Be(StockMovementType.NegativeAdjust);
        ctx.Movimientos[0].Quantity.Should().Be(-10m);
        ctx.Movimientos[0].PreviousQuantity.Should().Be(30m);

        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ejecutar_disminucion_con_stock_insuficiente_retorna_failure_y_rollback()
    {
        var ctx = new TestContext(cantidadAjuste: -20m);
        ctx.WithDecrementoFallido(); // stock insuficiente o carrera concurrente

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");

        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        ctx.Movimientos.Should().BeEmpty();
    }

    [Fact]
    public async Task Ejecutar_disminucion_no_llama_IncrementarAtomico()
    {
        var ctx = new TestContext(cantidadAjuste: -5m);
        ctx.WithDecrementoExitoso(cantAnterior: 10m);

        await ctx.Handle();

        ctx.StockRepo.Verify(x => x.IncrementStockAtomicAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Estados inválidos ─────────────────────────────────────────────────

    [Fact]
    public async Task Ejecutar_ajuste_ya_ejecutado_retorna_failure()
    {
        var ctx = new TestContext(cantidadAjuste: 5m);
        ctx.Ajuste.Execute(ctx.UserId);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Draft");
    }

    [Fact]
    public async Task Ejecutar_ajuste_cancelado_retorna_failure()
    {
        var ctx = new TestContext(cantidadAjuste: 5m);
        ctx.Ajuste.Cancel(ctx.UserId);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Draft");
    }

    [Fact]
    public async Task Ejecutar_ajuste_no_encontrado_retorna_failure()
    {
        var ctx = new TestContext(cantidadAjuste: 5m);
        ctx.AjusteRepo.Setup(x => x.GetByIdAsync(
                ctx.SubscriberId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockAdjustment?)null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrado");
    }

    // ── Contexto de test ──────────────────────────────────────────────────

    private sealed class TestContext
    {
        public Guid SubscriberId  { get; } = Guid.NewGuid();
        public Guid UserId    { get; } = Guid.NewGuid();
        public Guid BodegaId  { get; } = Guid.NewGuid();
        public Guid ProductoId { get; } = Guid.NewGuid();

        public StockAdjustment Ajuste { get; }

        public Mock<IStockAdjustmentRepository> AjusteRepo { get; } = new();
        public Mock<IStockRepository>  StockRepo  { get; } = new();
        public Mock<IUnitOfWork>                 Uow        { get; } = new();

        public List<StockMovement> Movimientos { get; } = [];

        private readonly Mock<IUserActivityRepository> _activity = new();
        private readonly Mock<ICostoPromedioService>   _costo    = new();
        private readonly Mock<ICurrentSubscriber>          _tenant   = new();
        private readonly Mock<ICurrentUser>            _user     = new();

        public TestContext(decimal cantidadAjuste)
        {
            _costo.Setup(x => x.ObtenerCostoPromedioAsync(
                    SubscriberId, ProductoId, BodegaId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0m);

            _tenant.SetupGet(x => x.SubscriberId).Returns(SubscriberId);
            _user.SetupGet(x => x.UserId).Returns(UserId);
            _user.SetupGet(x => x.Email).Returns("test@erp.dev");
            _user.SetupGet(x => x.FullName).Returns("Test User");

            Ajuste = StockAdjustment.Create(
                SubscriberId, sequential: 1,
                BodegaId, "Bodega Test",
                ProductoId, "Producto Test",
                cantidadAjuste, "Ajuste físico", null, UserId);

            AjusteRepo.Setup(x => x.GetByIdAsync(SubscriberId, Ajuste.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Ajuste);
            AjusteRepo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StockRepo.Setup(x => x.AddMovementAsync(
                    It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()))
                .Callback<StockMovement, CancellationToken>((m, _) => Movimientos.Add(m))
                .Returns(Task.CompletedTask);

            Uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _activity.Setup(x => x.AddAsync(
                It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public void WithIncrementoExitoso(decimal cantAnterior)
            => StockRepo.Setup(x => x.IncrementStockAtomicAsync(
                    SubscriberId, BodegaId, ProductoId, It.IsAny<decimal>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cantAnterior);

        public void WithDecrementoExitoso(decimal cantAnterior)
            => StockRepo.Setup(x => x.DecrementStockAtomicAsync(
                    SubscriberId, BodegaId, ProductoId, It.IsAny<decimal>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)cantAnterior);

        public void WithDecrementoFallido()
            => StockRepo.Setup(x => x.DecrementStockAtomicAsync(
                    SubscriberId, BodegaId, ProductoId, It.IsAny<decimal>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);

        public Task<Result<StockAdjustmentDto>> Handle()
        {
            var handler = new ExecuteStockAdjustmentCommandHandler(
                AjusteRepo.Object,
                StockRepo.Object,
                _costo.Object,
                _activity.Object,
                Uow.Object,
                _tenant.Object,
                _user.Object,
                NullLogger<ExecuteStockAdjustmentCommandHandler>.Instance);

            return handler.Handle(new ExecuteStockAdjustmentCommand(Ajuste.Id), CancellationToken.None);
        }
    }
}


