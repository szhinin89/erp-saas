using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.ConfirmarTransferencia;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Domain.Modules.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Inventario;

/// <summary>
/// Pruebas unitarias de ConfirmarTransferenciaCommandHandler con mocks de IInventarioStockRepository.
/// Verifican el movimiento atómico de stock y el manejo de transacciones,
/// incluyendo el escenario de stock insuficiente por concurrencia.
/// </summary>
public sealed class ConfirmarTransferenciaCommandHandlerTests
{
    // ── Casos de éxito ────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_mueve_stock_y_crea_movimientos_de_salida_y_entrada()
    {
        var ctx = new TestContext();
        ctx.WithDecrementoExitoso(cantAnteriorOrigen: 10m);
        ctx.WithIncrementoExitoso(cantAnteriorDestino: 0m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Estado.Should().Be("Confirmado");
        result.Value.FechaConfirmacion.Should().NotBeNull();

        // 2 movimientos: TransferenciaSalida + TransferenciaEntrada
        ctx.Movimientos.Should().HaveCount(2);
        ctx.Movimientos.Should().ContainSingle(
            m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaSalida && m.Cantidad == -5m);
        ctx.Movimientos.Should().ContainSingle(
            m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaEntrada && m.Cantidad == 5m);

        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_registra_cantidad_anterior_correcta_en_movimientos()
    {
        var ctx = new TestContext();
        ctx.WithDecrementoExitoso(cantAnteriorOrigen: 10m);
        ctx.WithIncrementoExitoso(cantAnteriorDestino: 3m);

        await ctx.Handle();

        // Movimiento salida: cantidadAnterior = 10 (stock antes del descuento)
        ctx.Movimientos.Should().ContainSingle(
            m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaSalida
              && m.CantidadAnterior == 10m);

        // Movimiento entrada: cantidadAnterior = 3 (stock destino antes del incremento)
        ctx.Movimientos.Should().ContainSingle(
            m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaEntrada
              && m.CantidadAnterior == 3m);
    }

    // ── Concurrencia ──────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_falla_si_decremento_atomico_retorna_null_por_concurrencia()
    {
        // Simula: la pre-verificación pasó, pero al ejecutar el UPDATE atómico
        // otro proceso ya consumió el stock (el UPDATE no afectó filas).
        var ctx = new TestContext();
        ctx.WithDecrementoFallido(); // DecrementarStockAtomicoAsync devuelve null

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");

        // Rollback obligatorio — ningún movimiento debe quedar registrado
        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        ctx.Movimientos.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar_falla_si_stock_insuficiente_directo()
    {
        // El decremento atómico devuelve null (stock = 0, se piden 5)
        var ctx = new TestContext();
        ctx.WithDecrementoFallido();

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Movimientos.Should().BeEmpty();
    }

    // ── Casos de estado inválido ──────────────────────────────────────────

    [Fact]
    public async Task Confirmar_falla_si_transferencia_no_existe()
    {
        var ctx = new TestContext();
        ctx.TransferenciaRepo.Setup(x => x.GetByIdAsync(
                ctx.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transferencia?)null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Confirmar_falla_si_transferencia_ya_esta_confirmada()
    {
        var ctx = new TestContext();
        ctx.Transferencia.Confirmar(ctx.UserId);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Borrador");
    }

    [Fact]
    public async Task Confirmar_falla_si_transferencia_esta_cancelada()
    {
        var ctx = new TestContext();
        ctx.Transferencia.Cancelar(ctx.UserId);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Borrador");
    }

    // ── Contexto de test ──────────────────────────────────────────────────

    private sealed class TestContext
    {
        public Guid TenantId       { get; } = Guid.NewGuid();
        public Guid UserId         { get; } = Guid.NewGuid();
        public Guid BodegaOrigenId  { get; } = Guid.NewGuid();
        public Guid BodegaDestinoId { get; } = Guid.NewGuid();
        public Guid ProductoId     { get; } = Guid.NewGuid();

        public Transferencia Transferencia { get; }

        public Mock<ITransferenciaRepository>   TransferenciaRepo { get; } = new();
        public Mock<IInventarioStockRepository> StockRepo         { get; } = new();
        public Mock<IUnitOfWork> Uow { get; } = new();

        public List<InventarioMovimiento> Movimientos { get; } = [];

        private readonly Mock<IProductRepository>      _productRepo = new();
        private readonly Mock<ICostoPromedioService>   _costo       = new();
        private readonly Mock<IUserActivityRepository> _activity    = new();
        private readonly Mock<ICurrentTenant>          _tenant      = new();
        private readonly Mock<ICurrentUser>            _user        = new();

        public TestContext()
        {
            _costo.Setup(x => x.ObtenerCostoPromedioAsync(
                    TenantId, ProductoId, BodegaOrigenId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0m);

            _tenant.SetupGet(x => x.TenantId).Returns(TenantId);
            _user.SetupGet(x => x.UserId).Returns(UserId);
            _user.SetupGet(x => x.Email).Returns("test@erp.dev");
            _user.SetupGet(x => x.FullName).Returns("Test User");

            // Transferencia en Borrador con un ítem de 5 unidades
            Transferencia = Transferencia.Create(
                TenantId, secuencial: 1,
                BodegaOrigenId, BodegaDestinoId,
                motivo: null, observaciones: null, createdBy: UserId);
            var detalle = TransferenciaDetalle.Create(
                TenantId, Transferencia.Id, ProductoId,
                cantidad: 5m, descripcion: "Producto test", createdBy: UserId);
            Transferencia.AgregarDetalle(detalle);

            TransferenciaRepo.Setup(x => x.GetByIdAsync(
                    TenantId, Transferencia.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transferencia);
            TransferenciaRepo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _productRepo.Setup(x => x.GetByIdAsync(
                    ProductoId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ERP.Domain.Products.Entities.Product?)null);

            StockRepo.Setup(x => x.AddMovimientoAsync(
                    It.IsAny<InventarioMovimiento>(), It.IsAny<CancellationToken>()))
                .Callback<InventarioMovimiento, CancellationToken>((m, _) => Movimientos.Add(m))
                .Returns(Task.CompletedTask);

            Uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _activity.Setup(x => x.AddAsync(
                It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        /// <summary>El decremento atómico tiene stock suficiente y retorna la cantidadAnterior.</summary>
        public void WithDecrementoExitoso(decimal cantAnteriorOrigen)
            => StockRepo.Setup(x => x.DecrementarStockAtomicoAsync(
                    TenantId, BodegaOrigenId, ProductoId, 5m, UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)cantAnteriorOrigen);

        /// <summary>El decremento atómico devuelve null → stock insuficiente (concurrencia o agotado).</summary>
        public void WithDecrementoFallido()
            => StockRepo.Setup(x => x.DecrementarStockAtomicoAsync(
                    TenantId, BodegaOrigenId, ProductoId, 5m, UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);

        /// <summary>El incremento atómico retorna la cantidadAnterior en destino.</summary>
        public void WithIncrementoExitoso(decimal cantAnteriorDestino)
            => StockRepo.Setup(x => x.IncrementarStockAtomicoAsync(
                    TenantId, BodegaDestinoId, ProductoId, 5m, UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cantAnteriorDestino);

        public Task<Result<TransferenciaDto>> Handle()
        {
            var handler = new ConfirmarTransferenciaCommandHandler(
                TransferenciaRepo.Object,
                StockRepo.Object,
                _costo.Object,
                _productRepo.Object,
                _activity.Object,
                Uow.Object,
                _tenant.Object,
                _user.Object,
                NullLogger<ConfirmarTransferenciaCommandHandler>.Instance);

            return handler.Handle(
                new ConfirmarTransferenciaCommand(Transferencia.Id),
                CancellationToken.None);
        }
    }
}
