using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.Support;
using ERP.Application.Inventory.UseCases.CancelAdjustment;
using ERP.Application.Inventory.UseCases.CreateAdjustment;
using ERP.Application.Inventory.UseCases.ExecuteAdjustment;
using ERP.Application.Modules.Inventory.UseCases.GetCurrentStockByWarehouse;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integraciÃ³n del flujo completo de Ajustes de Inventario.
/// Verifican que el stock se actualice correctamente y que el StockMovement quede registrado.
/// </summary>
public sealed class InventoryAdjustmentsEndToEndTests
{
    [Fact]
    public async Task Ejecutar_incremento_incrementa_stock_y_registra_movimiento_AjustePositivo()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 100m);

        // Crear ajuste +15 (sobrante)
        var crear = await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, AdjustmentQty: +15m,
            Reason: "Sobrante", Notes: null), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        crear.Value!.Status.Should().Be("Draft");
        crear.Value.AdjustmentNumber.Should().StartWith("ADJ-");

        // Ejecutar
        var ejecutar = await mediator.Send(
            new ExecuteStockAdjustmentCommand(crear.Value.Id), CancellationToken.None);

        ejecutar.IsSuccess.Should().BeTrue(ejecutar.Error);
        ejecutar.Value!.Status.Should().Be("Executed");

        // Stock debe ser 115
        var stockResult = await mediator.Send(
            new GetCurrentStockByWarehouseQuery(bodegaId, productoId), CancellationToken.None);
        stockResult.Value!.First(i => i.ProductId == productoId).AvailableQuantity.Should().Be(115m);

        // Movimiento registrado
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var movs = await db.StockMovements
            .Where(m => m.SubscriberId == subscriberId && m.SourceDocId == crear.Value.Id)
            .ToListAsync(CancellationToken.None);

        movs.Should().HaveCount(1);
        movs[0].MovementType.Should().Be(StockMovementType.PositiveAdjust);
        movs[0].Quantity.Should().Be(15m);
        movs[0].PreviousQuantity.Should().Be(100m);
        movs[0].ResultQuantity.Should().Be(115m);
    }

    [Fact]
    public async Task Ejecutar_disminucion_reduce_stock_y_registra_movimiento_AjusteNegativo()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 50m);

        var crear = await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, AdjustmentQty: -10m,
            Reason: "Merma", Notes: "Productos vencidos"), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var ejecutar = await mediator.Send(
            new ExecuteStockAdjustmentCommand(crear.Value!.Id), CancellationToken.None);

        ejecutar.IsSuccess.Should().BeTrue(ejecutar.Error);

        // Stock: 50 - 10 = 40
        var stockResult = await mediator.Send(
            new GetCurrentStockByWarehouseQuery(bodegaId, productoId), CancellationToken.None);
        stockResult.Value!.First(i => i.ProductId == productoId).AvailableQuantity.Should().Be(40m);

        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var mov = (await db.StockMovements
            .Where(m => m.SubscriberId == subscriberId && m.SourceDocId == crear.Value.Id)
            .ToListAsync(CancellationToken.None)).Single();

        mov.MovementType.Should().Be(StockMovementType.NegativeAdjust);
        mov.Quantity.Should().Be(-10m);
    }

    [Fact]
    public async Task Ejecutar_disminucion_sin_stock_suficiente_retorna_failure_sin_mover_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 5m);

        var crear = await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, AdjustmentQty: -5m,
            Reason: "Ajuste fÃ­sico", Notes: null), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        // Consumir el stock antes de ejecutar (simula concurrencia)
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var userId   = factory.MutableUser.UserId;
        var stock = db.CurrentStocks.First(s => s.SubscriberId == subscriberId
                                           && s.WarehouseId == bodegaId
                                           && s.ProductId == productoId);
        stock.ApplyMovement(-5m, userId); // agota el stock
        await db.SaveChangesAsync(CancellationToken.None);

        var ejecutar = await mediator.Send(
            new ExecuteStockAdjustmentCommand(crear.Value!.Id), CancellationToken.None);

        ejecutar.IsSuccess.Should().BeFalse();
        ejecutar.Error.Should().Contain("Stock insuficiente");

        // No debe haber movimientos registrados
        var movs = await db.StockMovements
            .Where(m => m.SubscriberId == subscriberId && m.SourceDocId == crear.Value.Id)
            .ToListAsync(CancellationToken.None);
        movs.Should().BeEmpty("no debe registrar movimientos si fallÃ³ la ejecuciÃ³n");
    }

    [Fact]
    public async Task CancelAdjustment_en_borrador_no_afecta_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 30m);

        var crear = await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, AdjustmentQty: -10m,
            Reason: "Robo", Notes: null), CancellationToken.None);

        var cancelar = await mediator.Send(
            new CancelStockAdjustmentCommand(crear.Value!.Id), CancellationToken.None);

        cancelar.IsSuccess.Should().BeTrue(cancelar.Error);
        cancelar.Value!.Status.Should().Be("Cancelled");

        // Stock intacto
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var stock = db.CurrentStocks.First(s => s.SubscriberId == subscriberId
                                           && s.WarehouseId == bodegaId
                                           && s.ProductId == productoId);
        stock.AvailableQuantity.Should().Be(30m);
    }

    [Fact]
    public async Task Ejecutar_ajuste_ya_ejecutado_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 20m);

        var crear = await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, +5m, "Ajuste fÃ­sico", null), CancellationToken.None);

        await mediator.Send(new ExecuteStockAdjustmentCommand(crear.Value!.Id), CancellationToken.None);

        // Segundo intento
        var segunda = await mediator.Send(
            new ExecuteStockAdjustmentCommand(crear.Value.Id), CancellationToken.None);

        segunda.IsSuccess.Should().BeFalse();
        segunda.Error.Should().Contain("Draft");
    }

    [Fact]
    public async Task CreateAdjustment_con_cantidad_cero_lanza_validacion()
    {
        // FluentValidation lanza ValidationException antes de llegar al handler.
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 10m);

        var act = async () => await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, AdjustmentQty: 0m,
            Reason: "Ajuste fÃ­sico", Notes: null), CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*cero*");
    }

    [Fact]
    public async Task CreateAdjustment_con_motivo_vacio_lanza_validacion()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 10m);

        var act = async () => await mediator.Send(new CreateStockAdjustmentCommand(
            bodegaId, productoId, AdjustmentQty: 5m,
            Reason: "", Notes: null), CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*motivo*");
    }

    // â”€â”€ Seed â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task<(Guid BodegaId, Guid ProductoId)> SeedAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory, decimal stockInicial)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var subscriberId   = seed.SubscriberId;
        var userId     = seed.UserId;
        var productoId = seed.ProductId;
        var bodegaId   = seed.WarehouseId;

        if (stockInicial > 0)
        {
            var stock = CurrentStock.Create(subscriberId, productoId, bodegaId, userId, companyId: seed.CompanyId);
            stock.ApplyMovement(stockInicial, userId);
            db.CurrentStocks.Add(stock);

            db.StockMovements.Add(StockMovement.Create(
                subscriberId, productoId, bodegaId,
                StockMovementType.PositiveAdjust,
                quantity:            stockInicial,
                previousQuantity:    0,
                reference:          "Stock inicial test ajuste",
                sourceDocId:   null,
                sourceDocType: null,
                createdBy:           userId,
                companyId:           seed.CompanyId));

            await db.SaveChangesAsync(CancellationToken.None);
        }

        return (bodegaId, productoId);
    }
}











