using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Ventas.UseCases.AnularFactura;
using ERP.Application.Ventas.UseCases.CrearVenta;
using ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;
using ERP.Application.Ventas.UseCases.GetVentaById;
using ERP.Application.Ventas.UseCases.ValidarVenta;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class VentasEndToEndTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static CrearVentaCommand BuildCrearVenta(
        Guid clienteId, Guid bodegaId, Guid sucursalId, Guid productoId, decimal cantidad)
        => new(clienteId, bodegaId, sucursalId,
               new List<ItemVentaDto> { new(productoId, cantidad, 25.00m) });

    private static async Task<(IMediator Mediator, ErpDbContext Db, IntegrationSeedData.SeedResult Seed, Guid ClienteId, Guid SucursalId)>
        SetupAsync(IntegrationTestWebAppFactory factory, decimal stockInicial = 10m)
    {
        var scope    = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var seed     = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial, ct: CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;
        return (mediator, db, seed, clienteId, sucursalId);
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Venta_completa_crea_valida_emite_y_descuenta_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory, stockInicial: 10m);

        // 1. Crear venta (2 unidades)
        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.BodegaId, sucursalId, seed.ProductId, cantidad: 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var ventaId = crear.Value;
        var facturaInicial = db.VentasFacturas.Find(ventaId)!;
        facturaInicial.Estado.Should().Be("Borrador");
        facturaInicial.ClaveAcceso.Should().HaveLength(49);
        facturaInicial.Secuencial.Should().Be("000000001");

        // 2. Validar
        var validar = await mediator.Send(new ValidarVentaCommand(ventaId), CancellationToken.None);
        validar.IsSuccess.Should().BeTrue(validar.Error);
        db.VentasFacturas.Find(ventaId)!.Estado.Should().Be("Validado");

        // 3. Emitir al SRI (simulado)
        var emitir = await mediator.Send(new EmitirFacturaElectronicaCommand(ventaId), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        db.Entry(db.VentasFacturas.Find(ventaId)!).Reload();
        var factura = db.VentasFacturas.Find(ventaId)!;
        factura.Estado.Should().Be("Autorizado");
        factura.NumeroAutorizacion.Should().NotBeNullOrEmpty();
        factura.FechaAutorizacion.Should().NotBeNull();
        factura.AsientoContableId.Should().NotBeNull("el asiento contable debe haberse creado");

        // 4. Consultar detalle via query
        var query = await mediator.Send(new GetVentaByIdQuery(ventaId), CancellationToken.None);
        query.IsSuccess.Should().BeTrue();
        query.Value!.Detalles.Should().HaveCount(1);
        query.Value.Total.Should().Be(50.00m); // 2 × 25.00, sin IVA

        // 5. Stock reducido: 10 - 2 = 8
        var stockFinal = db.StockActual.First(s =>
            s.TenantId == seed.TenantId && s.ProductoId == seed.ProductId && s.BodegaId == seed.BodegaId);
        stockFinal.Cantidad.Should().Be(8m);

        // 6. Movimiento SalidaVenta registrado
        var movSalida = db.InventarioMovimientos
            .Where(m => m.TenantId == seed.TenantId && m.TipoMovimiento == TipoMovimientoInventario.SalidaVenta)
            .ToList();
        movSalida.Should().HaveCount(1);
        movSalida[0].Cantidad.Should().Be(-2m);
        movSalida[0].CantidadResultante.Should().Be(8m);
        movSalida[0].DocumentoOrigenId.Should().Be(ventaId);
    }

    [Fact]
    public async Task Crear_venta_con_stock_insuficiente_retorna_error()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory, stockInicial: 1m);

        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.BodegaId, sucursalId, seed.ProductId, cantidad: 5m),
            CancellationToken.None);

        crear.IsSuccess.Should().BeFalse();
        crear.Error.Should().Contain("Stock insuficiente");
    }

    [Fact]
    public async Task Emitir_factura_ya_autorizada_retorna_error()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory);

        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.BodegaId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);
        var ventaId = crear.Value;

        await mediator.Send(new ValidarVentaCommand(ventaId), CancellationToken.None);
        var emitir1 = await mediator.Send(new EmitirFacturaElectronicaCommand(ventaId), CancellationToken.None);
        emitir1.IsSuccess.Should().BeTrue(emitir1.Error);

        // Segunda emisión debe fallar (ya está Autorizado)
        var emitir2 = await mediator.Send(new EmitirFacturaElectronicaCommand(ventaId), CancellationToken.None);
        emitir2.IsSuccess.Should().BeFalse();
        emitir2.Error.Should().Contain("Validada");
    }

    [Fact]
    public async Task Anular_factura_borrador_cambia_estado()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory);

        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.BodegaId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var anular = await mediator.Send(new AnularFacturaCommand(crear.Value), CancellationToken.None);
        anular.IsSuccess.Should().BeTrue(anular.Error);
        db.VentasFacturas.Find(crear.Value)!.Estado.Should().Be("Anulado");

        // Doble anulación debe fallar
        var anular2 = await mediator.Send(new AnularFacturaCommand(crear.Value), CancellationToken.None);
        anular2.IsSuccess.Should().BeFalse();
    }
}
