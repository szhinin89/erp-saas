using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.ConfirmarTransferencia;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Enums;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Inventario;

/// <summary>
/// Pruebas unitarias de ConfirmarTransferenciaCommandHandler con mocks de IInventarioStockRepository.
/// Verifican el movimiento atómico de stock y el manejo de transacciones.
/// </summary>
public sealed class ConfirmarTransferenciaCommandHandlerTests
{
    // ── Casos de éxito ────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_mueve_stock_y_crea_movimientos_de_salida_y_entrada()
    {
        var ctx = new TestContext(stockOrigenInicial: 10m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Estado.Should().Be("Confirmado");
        result.Value.FechaConfirmacion.Should().NotBeNull();

        // Debe crearse 1 movimiento de salida y 1 de entrada
        ctx.Movimientos.Should().HaveCount(2);
        ctx.Movimientos.Should().ContainSingle(
            m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaSalida && m.Cantidad == -5m);
        ctx.Movimientos.Should().ContainSingle(
            m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaEntrada && m.Cantidad == 5m);

        // La transacción debe cerrarse con Commit, no con Rollback
        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_crea_stock_destino_cuando_no_existe_registro_previo()
    {
        var ctx = new TestContext(stockOrigenInicial: 10m, stockDestinoInicial: null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);

        // Se debe haber creado el registro de StockActual en destino
        ctx.StockRepo.Verify(x => x.AddStockActualAsync(
            It.Is<StockActual>(s => s.BodegaId == ctx.BodegaDestinoId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_descuenta_stock_origen_e_incrementa_stock_destino()
    {
        var ctx = new TestContext(stockOrigenInicial: 10m, stockDestinoInicial: 3m);

        await ctx.Handle();

        // Origen: 10 - 5 = 5
        ctx.StockOrigen.CantidadDisponible.Should().Be(5m);
        // Destino: 3 + 5 = 8
        ctx.StockDestino!.CantidadDisponible.Should().Be(8m);
    }

    // ── Casos de fallo ────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_falla_si_stock_insuficiente_y_hace_rollback()
    {
        var ctx = new TestContext(stockOrigenInicial: 2m); // solo 2, ítem pide 5

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");

        // Rollback — ningún stock se modifica efectivamente
        ctx.Uow.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

        // No se deben haber registrado movimientos
        ctx.Movimientos.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar_falla_si_transferencia_no_existe()
    {
        var ctx = new TestContext(stockOrigenInicial: 10m);
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
        var ctx = new TestContext(stockOrigenInicial: 10m);

        // Confirmar la entidad de dominio antes de enviarla al handler
        ctx.Transferencia.Confirmar(ctx.UserId);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Borrador");
    }

    [Fact]
    public async Task Confirmar_falla_si_transferencia_esta_cancelada()
    {
        var ctx = new TestContext(stockOrigenInicial: 10m);
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
        public StockActual   StockOrigen  { get; }
        public StockActual?  StockDestino { get; private set; }

        public Mock<ITransferenciaRepository>   TransferenciaRepo { get; } = new();
        public Mock<IInventarioStockRepository> StockRepo         { get; } = new();

        public Mock<IUnitOfWork> Uow { get; } = new();

        public List<InventarioMovimiento> Movimientos { get; } = [];

        private readonly Mock<IProductRepository>      _productRepo = new();
        private readonly Mock<IUserActivityRepository> _activity    = new();
        private readonly Mock<ICurrentTenant>          _tenant      = new();
        private readonly Mock<ICurrentUser>            _user        = new();

        public TestContext(decimal stockOrigenInicial, decimal? stockDestinoInicial = null)
        {
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

            // Producto normal (no servicio, trackea stock)
            _productRepo.Setup(x => x.GetByIdAsync(
                    ProductoId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ERP.Domain.Products.Entities.Product?)null); // null → handler no saltea el check

            // Stock en bodega origen
            StockOrigen = StockActual.Create(TenantId, ProductoId, BodegaOrigenId, UserId);
            StockOrigen.AplicarMovimiento(stockOrigenInicial, UserId);

            StockRepo.Setup(x => x.GetStockByTenantBodegaProductAsync(
                    TenantId, BodegaOrigenId, ProductoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(StockOrigen);

            // Stock en bodega destino (opcional)
            if (stockDestinoInicial.HasValue)
            {
                StockDestino = StockActual.Create(TenantId, ProductoId, BodegaDestinoId, UserId);
                StockDestino.AplicarMovimiento(stockDestinoInicial.Value, UserId);
            }

            StockRepo.Setup(x => x.GetStockByTenantBodegaProductAsync(
                    TenantId, BodegaDestinoId, ProductoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(StockDestino);

            StockRepo.Setup(x => x.AddStockActualAsync(
                    It.IsAny<StockActual>(), It.IsAny<CancellationToken>()))
                .Callback<StockActual, CancellationToken>((s, _) => StockDestino = s)
                .Returns(Task.CompletedTask);

            StockRepo.Setup(x => x.AddMovimientoAsync(
                    It.IsAny<InventarioMovimiento>(), It.IsAny<CancellationToken>()))
                .Callback<InventarioMovimiento, CancellationToken>((m, _) => Movimientos.Add(m))
                .Returns(Task.CompletedTask);

            // UoW
            Uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Uow.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _activity.Setup(x => x.AddAsync(
                It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public Task<Result<TransferenciaDto>> Handle()
        {
            var handler = new ConfirmarTransferenciaCommandHandler(
                TransferenciaRepo.Object,
                StockRepo.Object,
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
