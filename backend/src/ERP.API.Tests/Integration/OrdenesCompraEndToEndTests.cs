using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.AprobarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.CancelarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.EnviarOrdenCompra;
using ERP.Application.Modules.Purchasing.UseCases.VincularFacturaAOrdenCompra;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integración del flujo completo de Órdenes de Compra (OC).
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

        var result = await mediator.Send(new CrearOrdenCompraCommand(
            proveedorId,
            FechaRequerida: DateTime.UtcNow.AddDays(30),
            BodegaDestinoId: null,
            DireccionEntrega: null,
            Observaciones: "Pedido prueba",
            Items: [new ItemOrdenCompraRequest(productoId, 10m, 15m, 15m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.NumeroOrden.Should().Be("OC-0001");
        result.Value.Estado.Should().Be("Borrador");
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

        var crear = await mediator.Send(new CrearOrdenCompraCommand(
            proveedorId, DateTime.UtcNow.AddDays(15), null, null, null,
            [new ItemOrdenCompraRequest(productoId, 5m, 20m, 15m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var enviar = await mediator.Send(
            new EnviarOrdenCompraCommand(crear.Value!.Id), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);
        enviar.Value!.Estado.Should().Be("Enviada");

        var aprobar = await mediator.Send(
            new AprobarOrdenCompraCommand(crear.Value.Id), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);
        aprobar.Value!.Estado.Should().Be("Aprobada");
    }

    [Fact]
    public async Task Cancelar_OC_en_Borrador_cambia_estado_a_Cancelada()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);

        var crear = await mediator.Send(new CrearOrdenCompraCommand(
            proveedorId, DateTime.UtcNow.AddDays(7), null, null, null,
            [new ItemOrdenCompraRequest(productoId, 2m, 10m, 15m)]),
            CancellationToken.None);

        var cancelar = await mediator.Send(
            new CancelarOrdenCompraCommand(crear.Value!.Id), CancellationToken.None);

        cancelar.IsSuccess.Should().BeTrue(cancelar.Error);
        cancelar.Value!.Estado.Should().Be("Cancelada");
    }

    [Fact]
    public async Task Vincular_factura_completa_cierra_la_OC()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);
        var tenantId = factory.MutableTenant.TenantId;
        var userId   = factory.MutableUser.UserId;

        // Crear y aprobar OC con 5 unidades
        var crear = await mediator.Send(new CrearOrdenCompraCommand(
            proveedorId, DateTime.UtcNow.AddDays(20), null, null, null,
            [new ItemOrdenCompraRequest(productoId, 5m, 10m, 15m)]),
            CancellationToken.None);
        await mediator.Send(new AprobarOrdenCompraCommand(crear.Value!.Id), CancellationToken.None);

        // Crear factura de compra Aprobada con 5 unidades del mismo producto
        var factura = BuildFacturaAprobada(tenantId, proveedorId, productoId, cantidad: 5m, userId, db);

        var vincular = await mediator.Send(
            new VincularFacturaAOrdenCompraCommand(crear.Value.Id, factura.Id),
            CancellationToken.None);

        vincular.IsSuccess.Should().BeTrue(vincular.Error);
        vincular.Value!.Estado.Should().Be("Cerrada");
    }

    [Fact]
    public async Task Vincular_factura_parcial_pone_OC_en_RecibidaParcial()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (proveedorId, productoId) = await SeedAsync(db, factory);
        var tenantId = factory.MutableTenant.TenantId;
        var userId   = factory.MutableUser.UserId;

        // OC pide 10 unidades
        var crear = await mediator.Send(new CrearOrdenCompraCommand(
            proveedorId, DateTime.UtcNow.AddDays(20), null, null, null,
            [new ItemOrdenCompraRequest(productoId, 10m, 10m, 15m)]),
            CancellationToken.None);
        await mediator.Send(new AprobarOrdenCompraCommand(crear.Value!.Id), CancellationToken.None);

        // Factura solo trae 4 unidades
        var factura = BuildFacturaAprobada(tenantId, proveedorId, productoId, cantidad: 4m, userId, db);

        var vincular = await mediator.Send(
            new VincularFacturaAOrdenCompraCommand(crear.Value.Id, factura.Id),
            CancellationToken.None);

        vincular.IsSuccess.Should().BeTrue(vincular.Error);
        vincular.Value!.Estado.Should().Be("RecibidaParcial");
    }

    // ── Seed ──────────────────────────────────────────────────────────────

    private static async Task<(Guid ProveedorId, Guid ProductoId)> SeedAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var proveedor = Proveedor.Create(
            seed.TenantId, "Juridica", "Proveedor Test S.A.",
            seed.ProveedorRuc, correo: null, telefono: null, direccion: null,
            "30 dias", seed.UserId);
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(CancellationToken.None);

        return (proveedor.Id, seed.ProductId);
    }

    private static CompraFactura BuildFacturaAprobada(
        Guid tenantId, Guid proveedorId, Guid productoId, decimal cantidad,
        Guid userId, ErpDbContext db)
    {
        var numero = $"001-001-{Guid.NewGuid().ToString()[..8]}";
        var f = CompraFactura.Create(
            tenantId, proveedorId, numero,
            claveAcceso: null, xmlPath: null,
            DateTime.UtcNow, fechaVencimiento: null,
            "30 dias", observaciones: null, userId);

        f.AgregarDetalle(
            "Producto Test E2E", codigoPrincipalProveedor: null, productoId,
            cantidad, precioUnitario: 10m,
            descuentoPorcentaje: 0m, ivaPorcentaje: 15m, userId);

        f.Validar(userId);
        f.Aprobar(userId, asientoContableId: null, Array.Empty<CompraAprobadaStockLine>());

        db.CompraFacturas.Add(f);
        db.SaveChanges();
        return f;
    }
}
