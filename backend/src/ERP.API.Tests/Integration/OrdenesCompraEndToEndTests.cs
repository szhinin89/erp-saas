using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.ApprovePurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.CancelPurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.SendPurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.LinkInvoiceToPurchaseOrder;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integraciÃƒÂ³n del flujo completo de Ãƒâ€œrdenes de Compra (OC).
/// Usan EF InMemory; no persisten entre tests.
/// </summary>
public sealed class OrdenesCompraEndToEndTests
{
    [Fact]
    public async Task Crear_OC_genera_numero_correlativo_y_queda_en_Borrador()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);

        var result = await mediator.Send(new CreatePurchaseOrderCommand(
            proveedorId,
            RequiredDate: DateTime.UtcNow.AddDays(30),
            TargetWarehouseId: null,
            DeliveryAddress: null,
            Notes: "Pedido prueba",
            Items: [new PurchaseOrderItemRequest(productoId, 10m, 15m, 15m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.OrderNumber.Should().Be("PO-0001");
        result.Value.Status.Should().Be("Draft");
        result.Value.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Enviar_y_Aprobar_cambia_estado_correctamente()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);

        var crear = await mediator.Send(new CreatePurchaseOrderCommand(
            proveedorId, DateTime.UtcNow.AddDays(15), null, null, null,
            [new PurchaseOrderItemRequest(productoId, 5m, 20m, 15m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var enviar = await mediator.Send(
            new SendOrderPurchaseCommand(crear.Value!.Id), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);
        enviar.Value!.Status.Should().Be("Sent");

        var aprobar = await mediator.Send(
            new ApproveOrderPurchaseCommand(crear.Value.Id), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);
        aprobar.Value!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Cancelar_OC_en_Borrador_cambia_estado_a_Cancelada()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);

        var crear = await mediator.Send(new CreatePurchaseOrderCommand(
            proveedorId, DateTime.UtcNow.AddDays(7), null, null, null,
            [new PurchaseOrderItemRequest(productoId, 2m, 10m, 15m)]),
            CancellationToken.None);

        var cancelar = await mediator.Send(
            new CancelOrderPurchaseCommand(crear.Value!.Id), CancellationToken.None);

        cancelar.IsSuccess.Should().BeTrue(cancelar.Error);
        cancelar.Value!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Vincular_factura_completa_cierra_la_OC()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var userId   = factory.MutableUser.UserId;

        // Crear y aprobar OC con 5 unidades
        var crear = await mediator.Send(new CreatePurchaseOrderCommand(
            proveedorId, DateTime.UtcNow.AddDays(20), null, null, null,
            [new PurchaseOrderItemRequest(productoId, 5m, 10m, 15m)]),
            CancellationToken.None);
        await mediator.Send(new ApproveOrderPurchaseCommand(crear.Value!.Id), CancellationToken.None);

        // Crear factura de compra Aprobada con 5 unidades del mismo producto
        var factura = BuildFacturaAprobada(subscriberId, proveedorId, productoId, quantity: 5m, userId, db);

        var vincular = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(crear.Value.Id, factura.Id),
            CancellationToken.None);

        vincular.IsSuccess.Should().BeTrue(vincular.Error);
        vincular.Value!.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task Vincular_factura_parcial_pone_OC_en_RecibidaParcial()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var userId   = factory.MutableUser.UserId;

        // OC pide 10 unidades
        var crear = await mediator.Send(new CreatePurchaseOrderCommand(
            proveedorId, DateTime.UtcNow.AddDays(20), null, null, null,
            [new PurchaseOrderItemRequest(productoId, 10m, 10m, 15m)]),
            CancellationToken.None);
        await mediator.Send(new ApproveOrderPurchaseCommand(crear.Value!.Id), CancellationToken.None);

        // Factura solo trae 4 unidades
        var factura = BuildFacturaAprobada(subscriberId, proveedorId, productoId, quantity: 4m, userId, db);

        var vincular = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(crear.Value.Id, factura.Id),
            CancellationToken.None);

        vincular.IsSuccess.Should().BeTrue(vincular.Error);
        vincular.Value!.Status.Should().Be("PartiallyReceived");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Seed Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private static async Task<(Guid ProveedorId, Guid ProductoId)> SeedAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var proveedor = BusinessPartner.Create(
            subscriberId:         seed.SubscriberId,
            identificationType:   "04",
            identificationNumber: seed.ProveedorRuc,
            personType:           ERP.Domain.MasterData.Enums.PersonType.Legal,
            legalName:            "Supplier Test S.A.",
            createdBy:            seed.UserId);
        db.BusinessPartners.Add(proveedor);
        await db.SaveChangesAsync(CancellationToken.None);

        return (proveedor.Id, seed.ProductId);
    }

    private static PurchBill BuildFacturaAprobada(
        Guid subscriberId, Guid proveedorId, Guid productoId, decimal quantity,
        Guid userId, ErpDbContext db)
    {
        var numero = $"001-001-{Guid.NewGuid().ToString()[..8]}";
        var f = PurchBill.Create(
            subscriberId, proveedorId, numero,
            accessKey: null, xmlPath: null,
            DateTime.UtcNow, dueDate: null,
            "30 dias", notes: null, userId);

        f.AddLine(
            "Producto Test E2E", supplierProductCode: null, productoId,
            quantity, unitPrice: 10m,
            discountPct: 0m, vatPct: 15m, userId);

        f.Validate(userId);
        f.Approve(userId, journalEntryId: null, Array.Empty<PurchBillApprovedStockLine>());

        db.PurchBills.Add(f);
        db.SaveChanges();
        return f;
    }
}









