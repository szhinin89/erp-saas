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
/// Prueba de integración del flujo manual completo de OC con dos productos:
/// Crear → Enviar → Aprobar → Vincular parcial → RecibidaParcial → Vincular resto → Cerrada
/// Luego intento de vincular más cantidad → falla.
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

        // ── Seed ──────────────────────────────────────────────────────────────
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var tenantId   = seed.TenantId;
        var userId     = seed.UserId;
        var productoAId = seed.ProductId; // Producto A (ya en seed)
        var productoBId = await SeedSegundoProductoAsync(db, seed); // Producto B

        var proveedor = Proveedor.Create(
            tenantId, "Juridica", "Proveedor Test S.A.",
            seed.ProveedorRuc, null, null, null, "30 dias", userId);
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(CancellationToken.None);

        // ── PASO 1: Crear OC con Producto A (10 uds × $5) y Producto B (5 uds × $10) ──
        var crear = await mediator.Send(new CrearOrdenCompraCommand(
            proveedor.Id,
            DateTime.UtcNow.AddDays(30),
            BodegaDestinoId: null,
            DireccionEntrega: null,
            Observaciones: "OC prueba flujo completo",
            Items:
            [
                new ItemOrdenCompraRequest(productoAId, Cantidad: 10m, PrecioUnitario: 5m,  IvaPorcentaje: 15m),
                new ItemOrdenCompraRequest(productoBId, Cantidad:  5m, PrecioUnitario: 10m, IvaPorcentaje: 15m),
            ]), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var oc = crear.Value!;
        oc.Estado.Should().Be("Borrador");
        oc.NumeroOrden.Should().Be("OC-0001");

        // Subtotal = 10*5 + 5*10 = 100; IVA 15% = 15; Total = 115
        oc.Subtotal.Should().Be(100m);
        oc.Impuesto.Should().Be(15m);
        oc.Total.Should().Be(115m);

        // ── PASO 2: Enviar OC ─────────────────────────────────────────────────
        var enviar = await mediator.Send(new EnviarOrdenCompraCommand(oc.Id), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);
        enviar.Value!.Estado.Should().Be("Enviada");

        // ── PASO 3: Aprobar OC ────────────────────────────────────────────────
        var aprobar = await mediator.Send(new AprobarOrdenCompraCommand(oc.Id), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);
        aprobar.Value!.Estado.Should().Be("Aprobada");

        // ── PASO 4: Factura 1 — A:6 uds, B:5 uds (parcial en A, total en B) ──
        var factura1 = BuildFacturaAprobada(tenantId, proveedor.Id,
            productoAId, cantidadA: 6m,
            productoBId, cantidadB: 5m,
            userId, db, "001-001-000000001");

        var vincular1 = await mediator.Send(
            new VincularFacturaAOrdenCompraCommand(oc.Id, factura1.Id), CancellationToken.None);

        vincular1.IsSuccess.Should().BeTrue(vincular1.Error);
        vincular1.Value!.Estado.Should().Be("RecibidaParcial",
            "B ya está completo pero A tiene 4 pendientes → RecibidaParcial");

        // Verificar detalles vía GetById
        var detalle1 = (await mediator.Send(new GetOrdenCompraByIdQuery(oc.Id), CancellationToken.None)).Value!;
        var lineaA1  = detalle1.Detalles.First(d => d.ProductoId == productoAId);
        var lineaB1  = detalle1.Detalles.First(d => d.ProductoId == productoBId);

        lineaA1.CantidadFacturada.Should().Be(6m,  "se facturaron 6 de A");
        lineaA1.PendienteFacturar.Should().Be(4m,  "faltan 4 de A");
        lineaB1.CantidadFacturada.Should().Be(5m,  "se facturaron 5 de B");
        lineaB1.PendienteFacturar.Should().Be(0m,  "B completamente cubierto");

        // ── PASO 5: Factura 2 — A:4 uds (completa el pedido de A) ────────────
        var factura2 = BuildFacturaAprobada(tenantId, proveedor.Id,
            productoAId, cantidadA: 4m,
            productoBId: null, cantidadB: 0m, // solo producto A
            userId, db, "001-001-000000002");

        var vincular2 = await mediator.Send(
            new VincularFacturaAOrdenCompraCommand(oc.Id, factura2.Id), CancellationToken.None);

        vincular2.IsSuccess.Should().BeTrue(vincular2.Error);
        vincular2.Value!.Estado.Should().Be("Cerrada",
            "A ya tiene 10/10, B tiene 5/5 → OC cierra completamente");

        // Verificar cantidades finales
        var detalleFinal = (await mediator.Send(new GetOrdenCompraByIdQuery(oc.Id), CancellationToken.None)).Value!;
        var lineaAFinal  = detalleFinal.Detalles.First(d => d.ProductoId == productoAId);
        lineaAFinal.CantidadFacturada.Should().Be(10m);
        lineaAFinal.PendienteFacturar.Should().Be(0m);

        // Verificar facturas vinculadas
        detalleFinal.FacturasVinculadas.Should().HaveCount(2);

        // ── PASO 6: Intentar vincular más cantidad de A → debe fallar ─────────
        var facturaExtra = BuildFacturaAprobada(tenantId, proveedor.Id,
            productoAId, cantidadA: 1m,
            productoBId: null, cantidadB: 0m,
            userId, db, "001-001-000000003");

        var vincularExtra = await mediator.Send(
            new VincularFacturaAOrdenCompraCommand(oc.Id, facturaExtra.Id), CancellationToken.None);

        vincularExtra.IsSuccess.Should().BeFalse(
            "la OC está Cerrada — no puede recibir más facturas");
        vincularExtra.Error.Should().Contain("Aprobada",
            "el handler rechaza OC fuera de estado Aprobada/RecibidaParcial");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedSegundoProductoAsync(
        ErpDbContext db, IntegrationSeedData.SeedResult seed)
    {
        // Reusar las mismas taxonomías del primer producto
        var line     = db.ProductLines.First(l => l.TenantId == seed.TenantId);
        var category = db.ProductCategories.First(c => c.TenantId == seed.TenantId);
        var sub      = db.ProductSubcategories.First(s => s.TenantId == seed.TenantId);
        var uom      = db.UnitsOfMeasure.First(u => u.TenantId == seed.TenantId);
        var brand    = db.Brands.First(b => b.TenantId == seed.TenantId);
        var ptype    = db.ProductTypes.First(t => t.TenantId == seed.TenantId);
        var tariff   = db.Tariffs.First(t => t.TenantId == seed.TenantId);

        var productoB = Product.Create(
            seed.TenantId,
            "SKU-INT-02", "Prod INT-B", "Producto B para prueba",
            line.Id, category.Id, sub.Id, uom.Id, brand.Id, ptype.Id, tariff.Id,
            appliesVatOnSale: false, saleTaxId: null, saleVatAccountId: null,
            appliesVatOnPurchase: false, purchaseTaxId: null, purchaseVatAccountId: null,
            seed.UserId,
            purchaseCode: "SKU-INT-02",
            isService: false,
            tracksStock: true);

        db.Products.Add(productoB);
        await db.SaveChangesAsync(CancellationToken.None);
        return productoB.Id;
    }

    private static CompraFactura BuildFacturaAprobada(
        Guid tenantId, Guid proveedorId,
        Guid productoAId, decimal cantidadA,
        Guid? productoBId, decimal cantidadB,
        Guid userId, ErpDbContext db,
        string numero)
    {
        var f = CompraFactura.Create(
            tenantId, proveedorId, numero,
            claveAcceso: null, xmlPath: null,
            DateTime.UtcNow, fechaVencimiento: null,
            "30 dias", observaciones: null, userId);

        f.AgregarDetalle("Producto A", null, productoAId, cantidadA, 5m,  0m, 15m, userId);

        if (productoBId.HasValue && cantidadB > 0)
            f.AgregarDetalle("Producto B", null, productoBId.Value, cantidadB, 10m, 0m, 15m, userId);

        f.Validar(userId);
        f.Aprobar(userId, asientoContableId: null, Array.Empty<CompraAprobadaStockLine>());

        db.CompraFacturas.Add(f);
        db.SaveChanges();
        return f;
    }
}
