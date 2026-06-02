using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.ApprovePurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.SendPurchaseOrder;
using ERP.Application.Modules.Purchasing.UseCases.GetPurchaseOrderById;
using ERP.Application.Modules.Purchasing.UseCases.LinkInvoiceToPurchaseOrder;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Prueba de integraciÃƒÂ³n del flujo manual completo de OC con dos productos:
/// Crear Ã¢â€ â€™ Enviar Ã¢â€ â€™ Aprobar Ã¢â€ â€™ Vincular parcial Ã¢â€ â€™ RecibidaParcial Ã¢â€ â€™ Vincular resto Ã¢â€ â€™ Cerrada
/// Luego intento de vincular mÃƒÂ¡s cantidad Ã¢â€ â€™ falla.
/// </summary>
public sealed class OrdenCompraFlujoCompletoTests
{
    [Fact]
    public async Task Flujo_completo_dos_productos_vinculacion_parcial_luego_cierre()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Ã¢â€â‚¬Ã¢â€â‚¬ Seed Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var subscriberId   = seed.SubscriberId;
        var userId     = seed.UserId;
        var productoAId = seed.ProductId; // Producto A (ya en seed)
        var productoBId = await SeedSegundoProductoAsync(db, seed); // Producto B

        var proveedor = BusinessPartner.Create(
            subscriberId:         subscriberId,
            identificationType:   "04",
            identificationNumber: seed.ProveedorRuc,
            legalName:            "Supplier Test S.A.",
            createdBy:            userId);
        db.BusinessPartners.Add(proveedor);
        await db.SaveChangesAsync(CancellationToken.None);

        // Ã¢â€â‚¬Ã¢â€â‚¬ PASO 1: Crear OC con Producto A (10 uds Ãƒâ€” $5) y Producto B (5 uds Ãƒâ€” $10) Ã¢â€â‚¬Ã¢â€â‚¬
        var crear = await mediator.Send(new CreatePurchaseOrderCommand(
            proveedor.Id,
            DateTime.UtcNow.AddDays(30),
            TargetWarehouseId: null,
            DeliveryAddress: null,
            Notes: "OC prueba flujo completo",
            Items:
            [
                new PurchaseOrderItemRequest(productoAId, Quantity: 10m, UnitPrice: 5m,  VatPct: 15m),
                new PurchaseOrderItemRequest(productoBId, Quantity:  5m, UnitPrice: 10m, VatPct: 15m),
            ]), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var oc = crear.Value!;
        oc.Status.Should().Be("Draft");
        oc.OrderNumber.Should().Be("PO-0001");

        // Subtotal = 10*5 + 5*10 = 100; IVA 15% = 15; Total = 115
        oc.Subtotal.Should().Be(100m);
        oc.VatTotal.Should().Be(15m);
        oc.Total.Should().Be(115m);

        // Ã¢â€â‚¬Ã¢â€â‚¬ PASO 2: Enviar OC Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        var enviar = await mediator.Send(new SendOrderPurchaseCommand(oc.Id), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);
        enviar.Value!.Status.Should().Be("Sent");

        // Ã¢â€â‚¬Ã¢â€â‚¬ PASO 3: Aprobar OC Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        var aprobar = await mediator.Send(new ApproveOrderPurchaseCommand(oc.Id), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);
        aprobar.Value!.Status.Should().Be("Approved");

        // Ã¢â€â‚¬Ã¢â€â‚¬ PASO 4: Factura 1 Ã¢â‚¬â€ A:6 uds, B:5 uds (parcial en A, total en B) Ã¢â€â‚¬Ã¢â€â‚¬
        var factura1 = BuildFacturaAprobada(subscriberId, proveedor.Id,
            productoAId, cantidadA: 6m,
            productoBId, cantidadB: 5m,
            userId, db, "001-001-000000001");

        var vincular1 = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(oc.Id, factura1.Id), CancellationToken.None);

        vincular1.IsSuccess.Should().BeTrue(vincular1.Error);
        vincular1.Value!.Status.Should().Be("PartiallyReceived",
            "B ya estÃƒÂ¡ completo pero A tiene 4 pendientes Ã¢â€ â€™ RecibidaParcial");

        // Verificar detalles vÃƒÂ­a GetById
        var detalle1 = (await mediator.Send(new GetPurchaseOrderByIdQuery(oc.Id), CancellationToken.None)).Value!;
        var lineaA1  = detalle1.Lines.First(d => d.ProductId == productoAId);
        var lineaB1  = detalle1.Lines.First(d => d.ProductId == productoBId);

        lineaA1.InvoicedQuantity.Should().Be(6m,  "se facturaron 6 de A");
        lineaA1.PendingBillingQuantity.Should().Be(4m,  "faltan 4 de A");
        lineaB1.InvoicedQuantity.Should().Be(5m,  "se facturaron 5 de B");
        lineaB1.PendingBillingQuantity.Should().Be(0m,  "B completamente cubierto");

        // Ã¢â€â‚¬Ã¢â€â‚¬ PASO 5: Factura 2 Ã¢â‚¬â€ A:4 uds (completa el pedido de A) Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        var factura2 = BuildFacturaAprobada(subscriberId, proveedor.Id,
            productoAId, cantidadA: 4m,
            productoBId: null, cantidadB: 0m, // solo producto A
            userId, db, "001-001-000000002");

        var vincular2 = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(oc.Id, factura2.Id), CancellationToken.None);

        vincular2.IsSuccess.Should().BeTrue(vincular2.Error);
        vincular2.Value!.Status.Should().Be("Closed",
            "A ya tiene 10/10, B tiene 5/5 Ã¢â€ â€™ OC cierra completamente");

        // Verificar cantidades finales
        var detalleFinal = (await mediator.Send(new GetPurchaseOrderByIdQuery(oc.Id), CancellationToken.None)).Value!;
        var lineaAFinal  = detalleFinal.Lines.First(d => d.ProductId == productoAId);
        lineaAFinal.InvoicedQuantity.Should().Be(10m);
        lineaAFinal.PendingBillingQuantity.Should().Be(0m);

        // Verificar facturas vinculadas
        detalleFinal.LinkedInvoices.Should().HaveCount(2);

        // Ã¢â€â‚¬Ã¢â€â‚¬ PASO 6: Intentar vincular mÃƒÂ¡s cantidad de A Ã¢â€ â€™ debe fallar Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        var facturaExtra = BuildFacturaAprobada(subscriberId, proveedor.Id,
            productoAId, cantidadA: 1m,
            productoBId: null, cantidadB: 0m,
            userId, db, "001-001-000000003");

        var vincularExtra = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(oc.Id, facturaExtra.Id), CancellationToken.None);

        vincularExtra.IsSuccess.Should().BeFalse(
            "la OC estÃƒÂ¡ Cerrada Ã¢â‚¬â€ no puede recibir mÃƒÂ¡s facturas");
        vincularExtra.Error.Should().Contain("PartiallyReceived",
            "el handler rechaza OC fuera de estado Approved/PartiallyReceived");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Helpers Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private static async Task<Guid> SeedSegundoProductoAsync(
        ErpDbContext db, IntegrationSeedData.SeedResult seed)
    {
        // Reusar las mismas taxonomÃƒÂ­as del primer producto
        var line     = db.ProductLines.First(l => l.SubscriberId == seed.SubscriberId);
        var category = db.ProductCategories.First(c => c.SubscriberId == seed.SubscriberId);
        var sub      = db.ProductSubcategories.First(s => s.SubscriberId == seed.SubscriberId);
        var uom      = db.UnitsOfMeasure.First(u => u.SubscriberId == seed.SubscriberId);
        var brand    = db.Brands.First(b => b.SubscriberId == seed.SubscriberId);
        var ptype    = db.ProductTypes.First(t => t.SubscriberId == seed.SubscriberId);
        var tariff   = db.Tariffs.First(t => t.SubscriberId == seed.SubscriberId);

        var productoB = Product.Create(
            seed.SubscriberId,
            "SKU-INT-02", "Prod INT-B", "Producto B para prueba",
            line.Id, category.Id, sub.Id, uom.Id, brand.Id, ptype.Id, tariff.Id,
            appliesVatOnSale: false, saleTaxId: null, saleVatAccountId: null,
            appliesVatOnPurchase: false, purchaseTaxId: null, purchaseVatAccountId: null,
            seed.UserId,
            purchaseCode: "SKU-INT-02",
            isService: false,
            tracksStock: true,
            companyId: seed.CompanyId);

        db.Products.Add(productoB);
        await db.SaveChangesAsync(CancellationToken.None);
        return productoB.Id;
    }

    private static PurchBill BuildFacturaAprobada(
        Guid subscriberId, Guid proveedorId,
        Guid productoAId, decimal cantidadA,
        Guid? productoBId, decimal cantidadB,
        Guid userId, ErpDbContext db,
        string numero)
    {
        var f = PurchBill.Create(
            subscriberId, proveedorId, numero,
            accessKey: null, xmlPath: null,
            DateTime.UtcNow, dueDate: null,
            "30 dias", notes: null, userId);

        f.AddLine("Producto A", null, productoAId, cantidadA, 5m,  0m, 15m, userId);

        if (productoBId.HasValue && cantidadB > 0)
            f.AddLine("Producto B", null, productoBId.Value, cantidadB, 10m, 0m, 15m, userId);

        f.Validate(userId);
        f.Approve(userId, journalEntryId: null, Array.Empty<PurchBillApprovedStockLine>());

        db.PurchBills.Add(f);
        db.SaveChanges();
        return f;
    }
}









