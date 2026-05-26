using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Sales.UseCases.CreateSale;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Unit;

/// <summary>
/// Pruebas de la lÃƒÂ³gica de validaciÃƒÂ³n de stock en CreateSaleCommandHandler.
/// Usan la misma infraestructura in-memory de IntegrationTestWebAppFactory
/// pero con datos mÃƒÂ­nimos y enfocados en el comportamiento de stock.
/// </summary>
public sealed class StockValidationHandlerTests
{
    [Fact]
    public async Task CreateSale_con_stock_suficiente_retorna_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 10m);

        var result = await mediator.Send(
            new CreateSaleCommand(clienteId, bodegaId, sucursalId,
                new List<SaleItemDto> { new(productoId, Quantity: 5m, UnitPrice: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task CreateSale_con_stock_exactamente_suficiente_retorna_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 3m);

        var result = await mediator.Send(
            new CreateSaleCommand(clienteId, bodegaId, sucursalId,
                new List<SaleItemDto> { new(productoId, Quantity: 3m, UnitPrice: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task CreateSale_con_stock_insuficiente_retorna_failure_con_detalle()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 2m);

        var result = await mediator.Send(
            new CreateSaleCommand(clienteId, bodegaId, sucursalId,
                new List<SaleItemDto> { new(productoId, Quantity: 5m, UnitPrice: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
        result.Error.Should().Contain("Disponible: 2");
        result.Error.Should().Contain("Solicitado: 5");
    }

    [Fact]
    public async Task CreateSale_sin_registro_de_stock_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Sin llamar a SeedMinimalDataAsync con stock Ã¢â‚¬â€ producto existe pero no tiene CurrentStocks
        var (clienteId, bodegaId, sucursalId, productoId) =
            await SeedMinimalDataAsync(db, factory, stockUnidades: 0m, crearStockActual: false);

        var result = await mediator.Send(
            new CreateSaleCommand(clienteId, bodegaId, sucursalId,
                new List<SaleItemDto> { new(productoId, Quantity: 1m, UnitPrice: 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
    }

    [Fact]
    public async Task CreateSale_con_varios_items_falla_si_uno_tiene_stock_insuficiente()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, ct: CancellationToken.None);

        var clienteId  = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        // Producto 1 con 10 unidades (OK), Producto 2 sin stock (falla)
        var prod2Id = seed.ProductId; // mismo producto Ã¢â‚¬â€ pedimos 100 con solo 10 en stock

        var result = await mediator.Send(
            new CreateSaleCommand(clienteId, seed.WarehouseId, sucursalId,
                new List<SaleItemDto>
                {
                    new(seed.ProductId, Quantity: 1m,   UnitPrice: 10m), // OK (10 Ã¢â€°Â¥ 1)
                    new(prod2Id,        Quantity: 100m,  UnitPrice: 10m), // FALLA (10 < 100)
                }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Setup helpers Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private static async Task<(Guid ClienteId, Guid BodegaId, Guid SucursalId, Guid ProductoId)>
        SeedMinimalDataAsync(
            ErpDbContext db,
            IntegrationTestWebAppFactory factory,
            decimal stockUnidades,
            bool crearStockActual = true)
    {
        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockUnidades, crearStockActual, CancellationToken.None);

        var clienteId  = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        return (clienteId, seed.WarehouseId, sucursalId, seed.ProductId);
    }
}





