using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.VincularFacturaAOrdenCompra;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Compras.Entities;
using ERP.Domain.Modules.Compras.Enums;
using ERP.Domain.Modules.Compras.Interfaces;
using ERP.Domain.Modules.Compras.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Compras;

public sealed class VincularFacturaAOrdenCompraCommandHandlerTests
{
    // ── Casos exitosos ────────────────────────────────────────────────────

    [Fact]
    public async Task Vincular_con_cobertura_total_cierra_la_OC()
    {
        var ctx = new TestContext();

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Estado.Should().Be("Cerrada");
        ctx.OrdenRepo.Verify(x => x.AddOrdenCompraFacturaAsync(
            It.IsAny<OrdenCompraFactura>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.UnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Vincular_con_cobertura_parcial_pasa_OC_a_RecibidaParcial()
    {
        // Pedimos 10 unidades, factura solo trae 5
        var ctx = new TestContext(cantidadPedida: 10m, cantidadFactura: 5m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Estado.Should().Be("RecibidaParcial");
    }

    // ── OC no encontrada / estado inválido ────────────────────────────────

    [Fact]
    public async Task Vincular_OC_no_encontrada_retorna_failure()
    {
        var ctx = new TestContext();
        ctx.OrdenRepo.Setup(x => x.GetByIdWithDetallesAsync(
                ctx.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrdenCompra?)null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Vincular_OC_en_Borrador_retorna_failure()
    {
        var ctx = new TestContext();
        var oc  = ctx.BuildOrdenCompra(aprobar: false); // estado Borrador
        ctx.OrdenRepo.Setup(x => x.GetByIdWithDetallesAsync(
                ctx.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oc);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Aprobada");
    }

    // ── Factura no encontrada / no aprobada ──────────────────────────────

    [Fact]
    public async Task Vincular_factura_no_encontrada_retorna_failure()
    {
        var ctx = new TestContext();
        ctx.CompraRepo.Setup(x => x.GetByIdWithDetailsAsync(
                ctx.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompraFactura?)null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Vincular_factura_no_aprobada_retorna_failure()
    {
        var ctx     = new TestContext();
        var factura = ctx.BuildFactura(aprobar: false); // estado Borrador
        ctx.CompraRepo.Setup(x => x.GetByIdWithDetailsAsync(
                ctx.TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(factura);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Aprobado");
    }

    // ── Vinculación duplicada ─────────────────────────────────────────────

    [Fact]
    public async Task Vincular_factura_ya_vinculada_retorna_failure()
    {
        var ctx = new TestContext();
        ctx.OrdenRepo.Setup(x => x.FacturaYaVinculadaAsync(
                ctx.TenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya está vinculada");
    }

    // ── Cantidad excedida ─────────────────────────────────────────────────

    [Fact]
    public async Task Vincular_factura_con_cantidad_excedida_retorna_failure()
    {
        // Pedimos 5, factura trae 10 → excede
        var ctx = new TestContext(cantidadPedida: 5m, cantidadFactura: 10m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("excede la cantidad pedida");
    }

    // ── Discrepancia de precio ────────────────────────────────────────────

    [Fact]
    public async Task Vincular_con_precio_factura_distinto_genera_advertencia()
    {
        // OC a $10, factura cobra $12 → diferencia 20% > 1% → advertencia
        var ctx = new TestContext(precioOC: 10m, precioFactura: 12m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Advertencias.Should().NotBeNullOrEmpty(
            "debe haber advertencia cuando el precio facturado difiere >1% del precio de OC");
        result.Value.Advertencias!.Should().ContainSingle();
        result.Value.Advertencias[0].Should().Contain("Discrepancia de precio");
        result.Value.Advertencias[0].Should().Contain("Producto Test");
    }

    [Fact]
    public async Task Vincular_con_precio_igual_no_genera_advertencias()
    {
        var ctx = new TestContext(precioOC: 10m, precioFactura: 10m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Advertencias.Should().BeNull(
            "no debe haber advertencias cuando los precios coinciden");
    }

    [Fact]
    public async Task Vincular_con_diferencia_de_precio_menor_al_umbral_no_genera_advertencia()
    {
        // OC a $10, factura $10.05 → diferencia 0.5% < 1% → sin advertencia
        var ctx = new TestContext(precioOC: 10m, precioFactura: 10.05m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Advertencias.Should().BeNull(
            "diferencia de 0.5% está dentro de la tolerancia del 1%");
    }

    // ── Contexto de test ──────────────────────────────────────────────────

    private sealed class TestContext
    {
        public Guid TenantId   { get; } = Guid.NewGuid();
        public Guid UserId     { get; } = Guid.NewGuid();
        public Guid ProductoId { get; } = Guid.NewGuid();
        public Guid ProveedorId { get; } = Guid.NewGuid();

        private readonly decimal _cantidadPedida;
        private readonly decimal _cantidadFactura;
        private readonly decimal _precioOC;
        private readonly decimal _precioFactura;

        public Mock<IOrdenCompraRepository> OrdenRepo  { get; } = new();
        public Mock<ICompraRepository>      CompraRepo { get; } = new();
        public Mock<IUnitOfWork>           UnitOfWork  { get; } = new();

        private readonly Mock<IProveedorRepository>    _proveedorRepo = new();
        private readonly Mock<IUserActivityRepository> _activity      = new();
        private readonly Mock<ICurrentTenant>          _tenant        = new();
        private readonly Mock<ICurrentUser>            _user          = new();

        private readonly Guid _ordenId   = Guid.NewGuid();
        private readonly Guid _facturaId = Guid.NewGuid();

        public TestContext(
            decimal cantidadPedida = 5m,
            decimal cantidadFactura = 5m,
            decimal precioOC = 10m,
            decimal precioFactura = 10m)
        {
            _cantidadPedida  = cantidadPedida;
            _cantidadFactura = cantidadFactura;
            _precioOC        = precioOC;
            _precioFactura   = precioFactura;

            _tenant.SetupGet(x => x.TenantId).Returns(TenantId);
            _user.SetupGet(x => x.UserId).Returns(UserId);
            _user.SetupGet(x => x.Email).Returns("test@erp.dev");
            _user.SetupGet(x => x.FullName).Returns("Test User");

            UnitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            UnitOfWork.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            UnitOfWork.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var orden   = BuildOrdenCompra(aprobar: true);
            var factura = BuildFactura(aprobar: true);

            _ordenId   = orden.Id;
            _facturaId = factura.Id;

            OrdenRepo.Setup(x => x.GetByIdWithDetallesAsync(TenantId, orden.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orden);
            OrdenRepo.Setup(x => x.FacturaYaVinculadaAsync(
                    TenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            OrdenRepo.Setup(x => x.AddOrdenCompraFacturaAsync(
                    It.IsAny<OrdenCompraFactura>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            CompraRepo.Setup(x => x.GetByIdWithDetailsAsync(TenantId, factura.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(factura);

            _proveedorRepo.Setup(x => x.GetByIdAsync(TenantId, ProveedorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ERP.Domain.Modules.Compras.Entities.Proveedor?)null);

            _activity.Setup(x => x.AddAsync(
                    It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public OrdenCompra BuildOrdenCompra(bool aprobar)
        {
            var oc = OrdenCompra.Create(
                TenantId, secuencial: 1, ProveedorId,
                DateTime.UtcNow.AddDays(30),
                bodegaDestinoId: null, direccionEntrega: null, observaciones: null,
                UserId);

            var detalle = OrdenCompraDetalle.Create(
                TenantId, oc.Id, ProductoId, "Producto Test",
                _cantidadPedida, precioUnitario: _precioOC, ivaPorcentaje: 15m, UserId);
            oc.AgregarDetalle(detalle);

            if (aprobar) oc.Aprobar(UserId);
            return oc;
        }

        public CompraFactura BuildFactura(bool aprobar)
        {
            var f = CompraFactura.Create(
                TenantId, ProveedorId, "001-001-000000001",
                claveAcceso: null, xmlPath: null,
                DateTime.UtcNow, fechaVencimiento: null,
                "30 dias", observaciones: null, UserId);

            f.AgregarDetalle(
                "Producto Test", null, ProductoId,
                _cantidadFactura, precioUnitario: _precioFactura,
                descuentoPorcentaje: 0m, ivaPorcentaje: 15m, UserId);

            if (aprobar)
            {
                f.Validar(UserId);
                f.Aprobar(UserId, asientoContableId: null);
            }
            return f;
        }

        public Task<Result<OrdenCompraDto>> Handle()
        {
            var handler = new VincularFacturaAOrdenCompraCommandHandler(
                OrdenRepo.Object,
                CompraRepo.Object,
                _proveedorRepo.Object,
                _activity.Object,
                _tenant.Object,
                _user.Object,
                UnitOfWork.Object,
                NullLogger<VincularFacturaAOrdenCompraCommandHandler>.Instance);

            return handler.Handle(
                new VincularFacturaAOrdenCompraCommand(_ordenId, _facturaId),
                CancellationToken.None);
        }
    }
}
