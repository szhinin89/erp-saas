using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventory.UseCases.CrearTransferencia;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Unit;

/// <summary>
/// Pruebas unitarias de la lÃ³gica de stock en CrearTransferenciaCommandHandler.
/// Verifican que la validaciÃ³n de stock de la bodega origen funcione correctamente.
/// </summary>
public sealed class TransferenciasStockTests
{
    [Fact]
    public async Task CrearTransferencia_con_stock_suficiente_retorna_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedBodegasYStockAsync(db, factory, stockOrigen: 10m);

        var result = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Reason: "ReubicaciÃ³n",
            Notes: null,
            Items: [new(productoId, Quantity: 5m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Borrador");
        result.Value.NumeroTransferencia.Should().StartWith("TR-");
    }

    [Fact]
    public async Task CrearTransferencia_con_stock_exactamente_suficiente_retorna_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedBodegasYStockAsync(db, factory, stockOrigen: 3m);

        var result = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 3m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task CrearTransferencia_con_stock_insuficiente_retorna_failure_con_detalle()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedBodegasYStockAsync(db, factory, stockOrigen: 2m);

        var result = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 5m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
        result.Error.Should().Contain("Disponible: 2");
        result.Error.Should().Contain("Solicitado: 5");
    }

    [Fact]
    public async Task CrearTransferencia_sin_registro_de_stock_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // stockOrigen: 0 sin crear CurrentStocks â†’ el producto existe pero no tiene stock
        var (origenId, destinoId, productoId) = await SeedBodegasYStockAsync(db, factory, stockOrigen: 0m, crearStock: false);

        var result = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 1m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Stock insuficiente");
    }

    [Fact]
    public async Task CrearTransferencia_bodega_origen_inexistente_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (_, destinoId, productoId) = await SeedBodegasYStockAsync(db, factory, stockOrigen: 10m);
        var bodegaInexistenteId = Guid.NewGuid();

        var result = await mediator.Send(new CrearTransferenciaCommand(
            bodegaInexistenteId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 1m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("bodega origen");
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Siembra dos bodegas activas y un producto con stock en la bodega origen.
    /// Retorna (bodegaOrigenId, bodegaDestinoId, productoId).
    /// </summary>
    private static async Task<(Guid OrigenId, Guid DestinoId, Guid ProductoId)>
        SeedBodegasYStockAsync(
            ErpDbContext db,
            IntegrationTestWebAppFactory factory,
            decimal stockOrigen,
            bool crearStock = true)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var tenantId  = seed.TenantId;
        var userId    = seed.UserId;
        var productoId = seed.ProductId;

        // La bodega creada en SeedAsync es la origen; creamos una segunda como destino.
        var branchId = db.Branches.First(b => b.TenantId == tenantId).Id;
        var destino  = Warehouse.Create(tenantId, branchId, "Warehouse Destino Test", null, null, userId);
        db.Warehouses.Add(destino);
        await db.SaveChangesAsync(CancellationToken.None);

        if (crearStock && stockOrigen > 0)
        {
            var stock = CurrentStock.Create(tenantId, productoId, seed.WarehouseId, userId);
            stock.ApplyMovement(stockOrigen, userId);
            db.CurrentStocks.Add(stock);

            var mov = StockMovement.Create(
                tenantId, productoId, seed.WarehouseId,
                StockMovementType.PositiveAdjust,
                quantity:            stockOrigen,
                previousQuantity:    0,
                reference:          "Stock inicial test transferencia",
                sourceDocId:   null,
                sourceDocType: null,
                createdBy:           userId);
            db.StockMovements.Add(mov);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return (seed.WarehouseId, destino.Id, productoId);
    }
}











