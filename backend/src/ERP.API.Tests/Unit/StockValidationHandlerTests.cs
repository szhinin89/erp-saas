using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Ventas.UseCases.CrearVenta;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Customers.Entities;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Unit;

/// <summary>
/// Pruebas de la lógica de validación de stock en CrearVentaCommandHandler.
/// Usan la misma infraestructura in-memory de IntegrationTestWebAppFactory
/// pero con datos mínimos y enfocados en el comportamiento de stock.
/// </summary>
public sealed class StockValidationHandlerTests
{
    [Fact]
    public async Task CrearVenta_con_stock_suficiente_retorna_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 10m);

        var result = await mediator.Send(
            new CrearVentaCommand(clienteId, bodegaId, sucursalId,
                new List<ItemVentaDto> { new(productoId, Cantidad: 5m, PrecioUnitario: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task CrearVenta_con_stock_exactamente_suficiente_retorna_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 3m);

        var result = await mediator.Send(
            new CrearVentaCommand(clienteId, bodegaId, sucursalId,
                new List<ItemVentaDto> { new(productoId, Cantidad: 3m, PrecioUnitario: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task CrearVenta_con_stock_insuficiente_retorna_failure_con_detalle()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 2m);

        var result = await mediator.Send(
            new CrearVentaCommand(clienteId, bodegaId, sucursalId,
                new List<ItemVentaDto> { new(productoId, Cantidad: 5m, PrecioUnitario: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
        result.Error.Should().Contain("Disponible: 2");
        result.Error.Should().Contain("Solicitado: 5");
    }

    [Fact]
    public async Task CrearVenta_sin_registro_de_stock_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Sin llamar a SeedMinimalDataAsync con stock — producto existe pero no tiene StockActual
        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 0m, crearStockActual: false);

        var result = await mediator.Send(
            new CrearVentaCommand(clienteId, bodegaId, sucursalId,
                new List<ItemVentaDto> { new(productoId, Cantidad: 1m, PrecioUnitario: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
    }

    [Fact]
    public async Task CrearVenta_con_varios_items_falla_si_uno_tiene_stock_insuficiente()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, ct: CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        // Producto 1 con 10 unidades (OK), Producto 2 sin stock (falla)
        var prod2Id = seed.ProductId; // mismo producto — pedimos 100 con solo 10 en stock

        var result = await mediator.Send(
            new CrearVentaCommand(clienteId, seed.BodegaId, sucursalId,
                new List<ItemVentaDto>
                {
                    new(seed.ProductId, Cantidad: 1m,   PrecioUnitario: 10m), // OK (10 ≥ 1)
                    new(prod2Id,        Cantidad: 100m,  PrecioUnitario: 10m), // FALLA (10 < 100)
                }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
    }

    // ── Setup helpers ─────────────────────────────────────────────────────

    private static async Task<(Guid ClienteId, Guid BodegaId, Guid SucursalId, Guid ProductoId)>
        SeedMinimalDataAsync(
            ErpDbContext db,
            IntegrationTestWebAppFactory factory,
            decimal stockUnidades,
            bool crearStockActual = true)
    {
        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockUnidades, crearStockActual, CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        return (clienteId, seed.BodegaId, sucursalId, seed.ProductId);
    }
}
