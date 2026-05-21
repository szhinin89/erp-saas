using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.AprobarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.EnviarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.GetOrdenCompraById;
using ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Prueba de integraciÃ³n del flujo manual completo de OC con dos productos:
/// Crear â†’ Enviar â†’ Aprobar â†’ Vincular parcial â†’ RecibidaParcial â†’ Vincular resto â†’ Cerrada
/// Luego intento de vincular mÃ¡s cantidad â†’ falla.
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

        // â”€â”€ Seed â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var subscriberId   = seed.SubscriberId;
        var userId     = seed.UserId;
        var productoAId = seed.ProductId; // Producto A (ya en seed)
        var productoBId = await SeedSegundoProductoAsync(db, seed); // Producto B

        var proveedor = Supplier.Create(
            subscriberId, "Legal", "Supplier Test S.A.",
            seed.ProveedorRuc, null, null, null, "30 dias", userId);
        db.Suppliers.Add(proveedor);
        await db.SaveChangesAsync(CancellationToken.None);

        // â”€â”€ PASO 1: Crear OC con Producto A (10 uds Ã— $5) y Producto B (5 uds Ã— $10) â”€â”€
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

        // â”€â”€ PASO 2: Enviar OC â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var enviar = await mediator.Send(new SendOrderPurchaseCommand(oc.Id), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);
        enviar.Value!.Status.Should().Be("Sent");

        // â”€â”€ PASO 3: Aprobar OC â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var aprobar = await mediator.Send(new ApproveOrderPurchaseCommand(oc.Id), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);
        aprobar.Value!.Status.Should().Be("Approved");

        // â”€â”€ PASO 4: Factura 1 â€” A:6 uds, B:5 uds (parcial en A, total en B) â”€â”€
        var factura1 = BuildFacturaAprobada(subscriberId, proveedor.Id,
            productoAId, cantidadA: 6m,
            productoBId, cantidadB: 5m,
            userId, db, "001-001-000000001");

        var vincular1 = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(oc.Id, factura1.Id), CancellationToken.None);

        vincular1.IsSuccess.Should().BeTrue(vincular1.Error);
        vincular1.Value!.Status.Should().Be("PartiallyReceived",
            "B ya estÃ¡ completo pero A tiene 4 pendientes â†’ RecibidaParcial");

        // Verificar detalles vÃ­a GetById
        var detalle1 = (await mediator.Send(new GetPurchaseOrderByIdQuery(oc.Id), CancellationToken.None)).Value!;
        var lineaA1  = detalle1.Lines.First(d => d.ProductId == productoAId);
        var lineaB1  = detalle1.Lines.First(d => d.ProductId == productoBId);

        lineaA1.InvoicedQuantity.Should().Be(6m,  "se facturaron 6 de A");
        lineaA1.PendingBillingQuantity.Should().Be(4m,  "faltan 4 de A");
        lineaB1.InvoicedQuantity.Should().Be(5m,  "se facturaron 5 de B");
        lineaB1.PendingBillingQuantity.Should().Be(0m,  "B completamente cubierto");

        // â”€â”€ PASO 5: Factura 2 â€” A:4 uds (completa el pedido de A) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var factura2 = BuildFacturaAprobada(subscriberId, proveedor.Id,
            productoAId, cantidadA: 4m,
            productoBId: null, cantidadB: 0m, // solo producto A
            userId, db, "001-001-000000002");

        var vincular2 = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(oc.Id, factura2.Id), CancellationToken.None);

        vincular2.IsSuccess.Should().BeTrue(vincular2.Error);
        vincular2.Value!.Status.Should().Be("Closed",
            "A ya tiene 10/10, B tiene 5/5 â†’ OC cierra completamente");

        // Verificar cantidades finales
        var detalleFinal = (await mediator.Send(new GetPurchaseOrderByIdQuery(oc.Id), CancellationToken.None)).Value!;
        var lineaAFinal  = detalleFinal.Lines.First(d => d.ProductId == productoAId);
        lineaAFinal.InvoicedQuantity.Should().Be(10m);
        lineaAFinal.PendingBillingQuantity.Should().Be(0m);

        // Verificar facturas vinculadas
        detalleFinal.LinkedInvoices.Should().HaveCount(2);

        // â”€â”€ PASO 6: Intentar vincular mÃ¡s cantidad de A â†’ debe fallar â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var facturaExtra = BuildFacturaAprobada(subscriberId, proveedor.Id,
            productoAId, cantidadA: 1m,
            productoBId: null, cantidadB: 0m,
            userId, db, "001-001-000000003");

        var vincularExtra = await mediator.Send(
            new LinkInvoiceToPurchaseOrderCommand(oc.Id, facturaExtra.Id), CancellationToken.None);

        vincularExtra.IsSuccess.Should().BeFalse(
            "la OC estÃ¡ Cerrada â€” no puede recibir mÃ¡s facturas");
        vincularExtra.Error.Should().Contain("PartiallyReceived",
            "el handler rechaza OC fuera de estado Approved/PartiallyReceived");
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task<Guid> SeedSegundoProductoAsync(
        ErpDbContext db, IntegrationSeedData.SeedResult seed)
    {
        // Reusar las mismas taxonomÃ­as del primer producto
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









