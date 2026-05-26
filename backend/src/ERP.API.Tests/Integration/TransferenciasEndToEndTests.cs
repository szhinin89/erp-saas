using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventory.UseCases.CancelTransfer;
using ERP.Application.Inventory.UseCases.ConfirmTransfer;
using ERP.Application.Inventory.UseCases.CreateTransfer;
using ERP.Application.Modules.Inventory.UseCases.GetCurrentStockByWarehouse;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integraciÃ³n del flujo completo de Transferencias entre bodegas.
/// Verifican que el stock se mueva correctamente al confirmar y que los
/// movimientos de inventario queden registrados.
/// </summary>
public sealed class TransferenciasEndToEndTests
{
    [Fact]
    public async Task ConfirmTransfer_mueve_stock_de_origen_a_destino_y_registra_movimientos()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        // 1. Crear en Borrador
        var crear = await mediator.Send(new CreateTransferCommand(
            origenId, destinoId,
            Reason: "ReposiciÃ³n bodega destino",
            Notes: null,
            Items: [new(productoId, Quantity: 4m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var transferenciaId = crear.Value!.Id;
        crear.Value.Status.Should().Be("Draft");

        // 2. Confirmar â€” mueve el stock
        var confirmar = await mediator.Send(
            new ConfirmTransferCommand(transferenciaId),
            CancellationToken.None);

        confirmar.IsSuccess.Should().BeTrue(confirmar.Error);
        confirmar.Value!.Status.Should().Be("Confirmed");
        confirmar.Value.ConfirmationDate.Should().NotBeNull();

        // 3. Verificar stock en bodega origen: 10 - 4 = 6
        var stockOrigenResult = await mediator.Send(
            new GetCurrentStockPorWarehouseQuery(origenId, productoId), CancellationToken.None);
        stockOrigenResult.IsSuccess.Should().BeTrue();
        var itemOrigen = stockOrigenResult.Value!.FirstOrDefault(i => i.ProductId == productoId);
        itemOrigen.Should().NotBeNull("el producto debe seguir teniendo stock en bodega origen");
        itemOrigen!.AvailableQuantity.Should().Be(6m);

        // 4. Verificar stock en bodega destino: 0 + 4 = 4
        var stockDestinoResult = await mediator.Send(
            new GetCurrentStockPorWarehouseQuery(destinoId, productoId), CancellationToken.None);
        stockDestinoResult.IsSuccess.Should().BeTrue();
        var itemDestino = stockDestinoResult.Value!.FirstOrDefault(i => i.ProductId == productoId);
        itemDestino.Should().NotBeNull("el producto debe tener stock en bodega destino tras la transferencia");
        itemDestino!.AvailableQuantity.Should().Be(4m);

        // 5. Verificar que se crearon exactamente 2 movimientos para esta transferencia
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var movimientos = await db.StockMovements
            .Where(m => m.SubscriberId == subscriberId
                     && m.SourceDocId == transferenciaId)
            .ToListAsync(CancellationToken.None);

        movimientos.Should().HaveCount(2, "debe haber un movimiento de salida y uno de entrada");
        movimientos.Should().ContainSingle(m => m.MovementType == StockMovementType.TransferExit
                                             && m.Quantity == -4m);
        movimientos.Should().ContainSingle(m => m.MovementType == StockMovementType.TransferEntry
                                             && m.Quantity == 4m);
    }

    [Fact]
    public async Task ConfirmTransfer_con_stock_insuficiente_retorna_failure_sin_mover_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 3m);

        // Crear con 3 unidades (OK para Borrador â€” valida en Crear)
        var crear = await mediator.Send(new CreateTransferCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 3m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        // Consumir el stock manualmente para simular una venta concurrente
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var userId   = factory.MutableUser.UserId;
        var stockEntity = db.CurrentStocks.First(s => s.SubscriberId == subscriberId
                                                  && s.WarehouseId == origenId
                                                  && s.ProductId == productoId);
        stockEntity.ApplyMovement(-3m, userId); // agota el stock
        await db.SaveChangesAsync(CancellationToken.None);

        // Confirmar ahora debe fallar
        var confirmar = await mediator.Send(
            new ConfirmTransferCommand(crear.Value!.Id),
            CancellationToken.None);

        confirmar.IsSuccess.Should().BeFalse();
        confirmar.Error.Should().Contain("Stock insuficiente");

        // El stock no debe haberse modificado (rollback efectivo)
        var stockPost = db.CurrentStocks.First(s => s.SubscriberId == subscriberId
                                               && s.WarehouseId == origenId
                                               && s.ProductId == productoId);
        stockPost.AvailableQuantity.Should().Be(0m);

        var movs = db.StockMovements
            .Where(m => m.SubscriberId == subscriberId && m.SourceDocId == crear.Value.Id)
            .ToList();
        movs.Should().BeEmpty("no se deben registrar movimientos si la confirmaciÃ³n fallÃ³");
    }

    [Fact]
    public async Task CancelTransfer_en_borrador_retorna_cancelado_sin_afectar_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        var crear = await mediator.Send(new CreateTransferCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 7m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var cancelar = await mediator.Send(
            new CancelTransferCommand(crear.Value!.Id),
            CancellationToken.None);

        cancelar.IsSuccess.Should().BeTrue(cancelar.Error);
        cancelar.Value!.Status.Should().Be("Cancelled");

        // El stock en la bodega origen debe permanecer intacto
        var subscriberId = factory.MutableSubscriber.SubscriberId;
        var stock = db.CurrentStocks.First(s => s.SubscriberId == subscriberId
                                           && s.WarehouseId == origenId
                                           && s.ProductId == productoId);
        stock.AvailableQuantity.Should().Be(10m, "cancelar no debe afectar el stock");
    }

    [Fact]
    public async Task ConfirmTransfer_ya_confirmada_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        var crear = await mediator.Send(new CreateTransferCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 2m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var id = crear.Value!.Id;

        var primera = await mediator.Send(new ConfirmTransferCommand(id), CancellationToken.None);
        primera.IsSuccess.Should().BeTrue(primera.Error);

        var segunda = await mediator.Send(new ConfirmTransferCommand(id), CancellationToken.None);
        segunda.IsSuccess.Should().BeFalse();
        segunda.Error.Should().Contain("Draft");
    }

    [Fact]
    public async Task CancelTransfer_ya_confirmada_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        var crear = await mediator.Send(new CreateTransferCommand(
            origenId, destinoId,
            Reason: null,
            Notes: null,
            Items: [new(productoId, Quantity: 2m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var id = crear.Value!.Id;

        await mediator.Send(new ConfirmTransferCommand(id), CancellationToken.None);

        var cancelar = await mediator.Send(new CancelTransferCommand(id), CancellationToken.None);
        cancelar.IsSuccess.Should().BeFalse();
        cancelar.Error.Should().Contain("Draft");
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task<(Guid OrigenId, Guid DestinoId, Guid ProductoId)>
        SeedAsync(
            ErpDbContext db,
            IntegrationTestWebAppFactory factory,
            decimal stockOrigen)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var subscriberId   = seed.SubscriberId;
        var userId     = seed.UserId;
        var productoId = seed.ProductId;

        var branchId = db.Branches.First(b => b.SubscriberId == subscriberId).Id;
        var destino = WarehouseTestHelpers.CreateSecondary(
            subscriberId, branchId, "Warehouse Destino E2E", "WDE", userId, seed.CompanyId);
        db.Warehouses.Add(destino);
        await db.SaveChangesAsync(CancellationToken.None);

        if (stockOrigen > 0)
        {
            var stock = CurrentStock.Create(subscriberId, productoId, seed.WarehouseId, userId, companyId: seed.CompanyId);
            stock.ApplyMovement(stockOrigen, userId);
            db.CurrentStocks.Add(stock);

            var mov = StockMovement.Create(
                subscriberId, productoId, seed.WarehouseId,
                StockMovementType.PositiveAdjust,
                quantity:            stockOrigen,
                previousQuantity:    0,
                reference:          "Stock inicial E2E transferencia",
                sourceDocId:   null,
                sourceDocType: null,
                createdBy:           userId,
                companyId:           seed.CompanyId);
            db.StockMovements.Add(mov);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return (seed.WarehouseId, destino.Id, productoId);
    }
}












