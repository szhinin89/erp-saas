using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Application.Inventario.UseCases.CrearTransferencia;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Bodegas.Entities;
using ERP.Domain.Bodegas.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Inventario;

/// <summary>
/// Pruebas unitarias de CrearTransferenciaCommandHandler con mocks de IInventarioStockRepository.
/// Verifican la lógica de validación de stock sin tocar la base de datos.
/// </summary>
public sealed class CrearTransferenciaCommandHandlerTests
{
    // ── Casos de stock insuficiente ────────────────────────────────────────

    [Fact]
    public async Task Crear_falla_cuando_stock_insuficiente_en_origen()
    {
        var ctx = new TestContext();
        ctx.WithStockOrigen(cantidad: 2m); // solo 2 unidades

        var result = await ctx.Handle(cantidadItem: 5m); // pide 5

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
        result.Error.Should().Contain("Disponible: 2");
        result.Error.Should().Contain("Solicitado: 5");
    }

    [Fact]
    public async Task Crear_falla_cuando_no_hay_registro_de_stock_en_origen()
    {
        var ctx = new TestContext();
        ctx.WithStockOrigen(null); // sin registro de stock

        var result = await ctx.Handle(cantidadItem: 1m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
    }

    [Fact]
    public async Task Crear_falla_si_bodega_origen_no_existe()
    {
        var ctx = new TestContext();
        ctx.BodegaOrigenRepo.Setup(x => x.GetByIdAsync(
                ctx.TenantId, ctx.BodegaOrigenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Bodega?)null);

        var result = await ctx.Handle(cantidadItem: 1m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("bodega origen");
    }

    [Fact]
    public async Task Crear_falla_si_bodega_destino_no_existe()
    {
        var ctx = new TestContext();
        ctx.BodegaOrigenRepo.Setup(x => x.GetByIdAsync(
                ctx.TenantId, ctx.BodegaDestinoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Bodega?)null);

        var result = await ctx.Handle(cantidadItem: 1m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("bodega destino");
    }

    [Fact]
    public async Task Crear_falla_si_producto_no_existe()
    {
        var ctx = new TestContext();
        ctx.ProductRepo.Setup(x => x.GetByIdAsync(
                ctx.ProductoId, ctx.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await ctx.Handle(cantidadItem: 1m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ctx.ProductoId.ToString());
    }

    // ── Casos de éxito ────────────────────────────────────────────────────

    [Fact]
    public async Task Crear_exito_con_stock_exactamente_suficiente()
    {
        var ctx = new TestContext();
        ctx.WithStockOrigen(cantidad: 3m);

        var result = await ctx.Handle(cantidadItem: 3m);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Estado.Should().Be("Borrador");
        result.Value.NumeroTransferencia.Should().StartWith("TR-");
    }

    [Fact]
    public async Task Crear_exito_con_stock_suficiente_y_retorna_dto_correcto()
    {
        var ctx = new TestContext();
        ctx.WithStockOrigen(cantidad: 10m);

        var result = await ctx.Handle(cantidadItem: 4m);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.BodegaOrigenId.Should().Be(ctx.BodegaOrigenId);
        result.Value.BodegaDestinoId.Should().Be(ctx.BodegaDestinoId);
        result.Value.Estado.Should().Be("Borrador");

        // Verificar que se guardó en repositorio
        ctx.TransferenciaRepo.Verify(x => x.AddAsync(
            It.IsAny<Transferencia>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.TransferenciaRepo.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Crear_no_verifica_stock_si_producto_es_servicio()
    {
        var ctx = new TestContext();
        // Producto configurado como servicio (no maneja stock físico)
        ctx.ProductRepo.Setup(x => x.GetByIdAsync(
                ctx.ProductoId, ctx.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx.BuildProducto(isService: true, tracksStock: false));

        var result = await ctx.Handle(cantidadItem: 999m); // cantidad irrelevante

        result.IsSuccess.Should().BeTrue(result.Error);

        // No debe haberse consultado el stock
        ctx.StockRepo.Verify(x => x.GetStockByTenantBodegaProductAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Contexto de test ──────────────────────────────────────────────────

    private sealed class TestContext
    {
        public Guid TenantId      { get; } = Guid.NewGuid();
        public Guid UserId        { get; } = Guid.NewGuid();
        public Guid BodegaOrigenId  { get; } = Guid.NewGuid();
        public Guid BodegaDestinoId { get; } = Guid.NewGuid();
        public Guid ProductoId    { get; } = Guid.NewGuid();

        public Mock<ITransferenciaRepository>    TransferenciaRepo { get; } = new();
        public Mock<IBodegaRepository>           BodegaOrigenRepo  { get; } = new();
        public Mock<IProductRepository>          ProductRepo       { get; } = new();
        public Mock<IInventarioStockRepository>  StockRepo         { get; } = new();

        private readonly Mock<IUserActivityRepository> _activity  = new();
        private readonly Mock<ICurrentTenant>          _tenant    = new();
        private readonly Mock<ICurrentUser>            _user      = new();

        public TestContext()
        {
            _tenant.SetupGet(x => x.TenantId).Returns(TenantId);
            _user.SetupGet(x => x.UserId).Returns(UserId);
            _user.SetupGet(x => x.Email).Returns("test@erp.dev");
            _user.SetupGet(x => x.FullName).Returns("Test User");

            // Bodegas activas por defecto
            var origen  = Bodega.Create(TenantId, Guid.NewGuid(), "Bodega Origen",  null, null, UserId);
            var destino = Bodega.Create(TenantId, Guid.NewGuid(), "Bodega Destino", null, null, UserId);

            BodegaOrigenRepo.Setup(x => x.GetByIdAsync(TenantId, BodegaOrigenId,  It.IsAny<CancellationToken>())).ReturnsAsync(origen);
            BodegaOrigenRepo.Setup(x => x.GetByIdAsync(TenantId, BodegaDestinoId, It.IsAny<CancellationToken>())).ReturnsAsync(destino);

            // Producto activo, trackea stock, no es servicio
            ProductRepo.Setup(x => x.GetByIdAsync(ProductoId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildProducto());

            // Secuencial = 1
            TransferenciaRepo.Setup(x => x.GetNextSecuencialAsync(TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            TransferenciaRepo.Setup(x => x.AddAsync(It.IsAny<Transferencia>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            TransferenciaRepo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _activity.Setup(x => x.AddAsync(
                It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        /// <summary>Configura el stock de la bodega origen.</summary>
        public void WithStockOrigen(decimal? cantidad)
        {
            StockActual? stock = null;
            if (cantidad.HasValue)
            {
                stock = StockActual.Create(TenantId, ProductoId, BodegaOrigenId, UserId);
                if (cantidad.Value > 0)
                    stock.AplicarMovimiento(cantidad.Value, UserId);
            }

            StockRepo.Setup(x => x.GetStockByTenantBodegaProductAsync(
                    TenantId, BodegaOrigenId, ProductoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stock);
        }

        public Product BuildProducto(bool isService = false, bool tracksStock = true)
        {
            var g = Guid.NewGuid;
            return Product.Create(
                TenantId, "SKU-TEST", "Prod Test", "Descripcion test",
                g(), g(), g(), g(), g(), g(), g(),
                appliesVatOnSale: false, saleTaxId: null, saleVatAccountId: null,
                appliesVatOnPurchase: false, purchaseTaxId: null, purchaseVatAccountId: null,
                createdBy: UserId,
                isService: isService,
                tracksStock: tracksStock);
        }

        public Task<Result<TransferenciaDto>> Handle(decimal cantidadItem)
        {
            var handler = new CrearTransferenciaCommandHandler(
                TransferenciaRepo.Object,
                BodegaOrigenRepo.Object,
                ProductRepo.Object,
                StockRepo.Object,
                _activity.Object,
                _tenant.Object,
                _user.Object,
                NullLogger<CrearTransferenciaCommandHandler>.Instance);

            return handler.Handle(
                new CrearTransferenciaCommand(
                    BodegaOrigenId, BodegaDestinoId,
                    Motivo: null, Observaciones: null,
                    Items: [new(ProductoId, cantidadItem)]),
                CancellationToken.None);
        }
    }
}
