using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.MasterData.Interfaces;
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
        result.Value!.Status.Should().Be("Closed");
        ctx.OrdenRepo.Verify(x => x.AddOrderBillLinkAsync(
            It.IsAny<PurchaseOrderBill>(), It.IsAny<CancellationToken>()), Times.Once);
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
        result.Value!.Status.Should().Be("PartiallyReceived");
    }

    // ── OC no encontrada / estado inválido ────────────────────────────────

    [Fact]
    public async Task Vincular_OC_no_encontrada_retorna_failure()
    {
        var ctx = new TestContext();
        ctx.OrdenRepo.Setup(x => x.GetByIdAsync(
                ctx.SubscriberId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrder?)null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Vincular_OC_en_Borrador_retorna_failure()
    {
        var ctx = new TestContext();
        var oc  = ctx.BuildOrdenCompra(aprobar: false); // estado Borrador
        ctx.OrdenRepo.Setup(x => x.GetByIdAsync(
                ctx.SubscriberId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oc);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Approved");
    }

    // ── Factura no encontrada / no aprobada ──────────────────────────────

    [Fact]
    public async Task Vincular_factura_no_encontrada_retorna_failure()
    {
        var ctx = new TestContext();
        ctx.CompraRepo.Setup(x => x.GetByIdAsync(
                ctx.SubscriberId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchBill?)null);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Vincular_factura_no_aprobada_retorna_failure()
    {
        var ctx     = new TestContext();
        var factura = ctx.BuildFactura(aprobar: false); // estado Borrador
        ctx.CompraRepo.Setup(x => x.GetByIdAsync(
                ctx.SubscriberId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(factura);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("IsApproved");
    }

    // ── Vinculación duplicada ─────────────────────────────────────────────

    [Fact]
    public async Task Vincular_factura_ya_vinculada_retorna_failure()
    {
        var ctx = new TestContext();
        ctx.OrdenRepo.Setup(x => x.BillAlreadyLinkedAsync(
                ctx.SubscriberId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
        result.Value!.Warnings.Should().NotBeNullOrEmpty(
            "debe haber advertencia cuando el precio facturado difiere >1% del precio de OC");
        result.Value.Warnings!.Should().ContainSingle();
        result.Value.Warnings[0].Should().Contain("Discrepancia de precio");
        result.Value.Warnings[0].Should().Contain("Producto Test");
    }

    [Fact]
    public async Task Vincular_con_precio_igual_no_genera_advertencias()
    {
        var ctx = new TestContext(precioOC: 10m, precioFactura: 10m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Warnings.Should().BeNull(
            "no debe haber advertencias cuando los precios coinciden");
    }

    [Fact]
    public async Task Vincular_con_diferencia_de_precio_menor_al_umbral_no_genera_advertencia()
    {
        // OC a $10, factura $10.05 → diferencia 0.5% < 1% → sin advertencia
        var ctx = new TestContext(precioOC: 10m, precioFactura: 10.05m);

        var result = await ctx.Handle();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Warnings.Should().BeNull(
            "diferencia de 0.5% está dentro de la tolerancia del 1%");
    }

    // ── Contexto de test ──────────────────────────────────────────────────

    private sealed class TestContext
    {
        public Guid SubscriberId   { get; } = Guid.NewGuid();
        public Guid UserId     { get; } = Guid.NewGuid();
        public Guid ProductoId { get; } = Guid.NewGuid();
        public Guid ProveedorId { get; } = Guid.NewGuid();

        private readonly decimal _cantidadPedida;
        private readonly decimal _cantidadFactura;
        private readonly decimal _precioOC;
        private readonly decimal _precioFactura;

        public Mock<IPurchaseOrderRepository> OrdenRepo  { get; } = new();
        public Mock<IPurchBillRepository>      CompraRepo { get; } = new();
        public Mock<IUnitOfWork>           UnitOfWork  { get; } = new();

        private readonly Mock<IBusinessPartnerRepository> _bpRepo = new();
        private readonly Mock<IUserActivityRepository> _activity      = new();
        private readonly Mock<ICurrentSubscriber>          _tenant        = new();
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

            _tenant.SetupGet(x => x.SubscriberId).Returns(SubscriberId);
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

            OrdenRepo.Setup(x => x.GetByIdAsync(SubscriberId, orden.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orden);
            OrdenRepo.Setup(x => x.BillAlreadyLinkedAsync(
                    SubscriberId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            OrdenRepo.Setup(x => x.AddOrderBillLinkAsync(
                    It.IsAny<PurchaseOrderBill>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            CompraRepo.Setup(x => x.GetByIdAsync(SubscriberId, factura.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(factura);

            _bpRepo.Setup(x => x.GetByIdAsync(ProveedorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ERP.Domain.MasterData.Entities.BusinessPartner?)null);

            _activity.Setup(x => x.AddAsync(
                    It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public PurchaseOrder BuildOrdenCompra(bool aprobar)
        {
            var oc = PurchaseOrder.Create(
                SubscriberId, sequential: 1, ProveedorId,
                DateTime.UtcNow.AddDays(30),
                targetWarehouseId: null, deliveryAddress: null, notes: null,
                UserId);

            var detalle = PurchaseOrderLine.Create(
                SubscriberId, oc.Id, ProductoId, "Producto Test",
                _cantidadPedida, unitCost: _precioOC, vatPct: 15m, UserId);
            oc.AddLine(detalle);

            if (aprobar) oc.Approve(UserId);
            return oc;
        }

        public PurchBill BuildFactura(bool aprobar)
        {
            var f = PurchBill.Create(
                SubscriberId, ProveedorId, "001-001-000000001",
                accessKey: null, xmlPath: null,
                DateTime.UtcNow, dueDate: null,
                "30 dias", notes: null, UserId);

            f.AddLine(
                "Producto Test", null, ProductoId,
                _cantidadFactura, unitPrice: _precioFactura,
                discountPct: 0m, vatPct: 15m, UserId);

            if (aprobar)
            {
                f.Validate(UserId);
                f.Approve(UserId, journalEntryId: null, Array.Empty<PurchBillApprovedStockLine>());
            }
            return f;
        }

        public Task<Result<PurchaseOrderDto>> Handle()
        {
            var handler = new LinkInvoiceToPurchaseOrderCommandHandler(
                OrdenRepo.Object,
                CompraRepo.Object,
                _bpRepo.Object,
                _activity.Object,
                _tenant.Object,
                _user.Object,
                UnitOfWork.Object,
                NullLogger<LinkInvoiceToPurchaseOrderCommandHandler>.Instance);

            return handler.Handle(
                new LinkInvoiceToPurchaseOrderCommand(_ordenId, _facturaId),
                CancellationToken.None);
        }
    }
}


