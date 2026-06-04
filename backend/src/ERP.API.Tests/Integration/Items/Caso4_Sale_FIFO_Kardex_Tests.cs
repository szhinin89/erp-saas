using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.GetLots;
using ERP.Application.Items.UseCases.RegisterLot;
// Note: ERP.Application.Costing.UseCases — GetItemAverageCost pending implementation
using ERP.Domain.Modules.Costing.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 4: Vender producto → FIFO → Kardex.
///
/// Estado de implementación:
/// ✅ CostLayer domain (FIFO consumption logic)
/// ✅ CostLayerRepository.GetFifoLayersAsync() + GetAvcoSummaryAsync()
/// ✅ GetLots query
/// ⚠️  Venta integrada (Sales → StockMovement → CostLayer.Consume()) pendiente.
///     El módulo de ventas aún referencia ProductId, no ItemId.
///     Por ahora se valida el algoritmo FIFO a nivel de dominio y repositorio.
///
/// PRÓXIMO PASO: Cuando el módulo de ventas sea actualizado para usar ItemId,
/// este test se extenderá para validar el flujo completo de venta.
/// </summary>
public sealed class Caso4_Sale_FIFO_Kardex_Tests
{
    // ── Test 4.1: FIFO — capa más antigua se consume primero ─────────────

    [Fact]
    public async Task FIFO_GetLayers_OrderedByDate_OldestFirst()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var costRepo = scope.ServiceProvider
            .GetRequiredService<ERP.Domain.Modules.Costing.Interfaces.ICostLayerRepository>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "FIFO-SALE-001", ShortName: "FIFO sale item", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var now = DateTime.UtcNow;

        // 3 purchase batches at different dates and costs (as would be created by PurchaseReceipt)
        var layers = new[]
        {
            ItemsTestSeedData.CreateCostLayer(seed.SubscriberId, seed.CompanyId,
                item.Id, seed.WarehouseId, 100m, 5.00m, seed.UserId, now.AddDays(-20)),
            ItemsTestSeedData.CreateCostLayer(seed.SubscriberId, seed.CompanyId,
                item.Id, seed.WarehouseId, 80m, 6.50m, seed.UserId, now.AddDays(-10)),
            ItemsTestSeedData.CreateCostLayer(seed.SubscriberId, seed.CompanyId,
                item.Id, seed.WarehouseId, 60m, 8.00m, seed.UserId, now.AddDays(-2)),
        };

        db.CostLayers.AddRange(layers);
        await db.SaveChangesAsync();

        // Get FIFO layers via repository
        var fifoLayers = await costRepo.GetFifoLayersAsync(
            item.Id, seed.WarehouseId, null, CancellationToken.None);

        fifoLayers.Should().HaveCount(3);
        fifoLayers[0].UnitCost.Should().Be(5.00m, "Oldest (cheapest) batch first");
        fifoLayers[1].UnitCost.Should().Be(6.50m);
        fifoLayers[2].UnitCost.Should().Be(8.00m, "Newest batch last");
        fifoLayers.Should().AllSatisfy(l => l.IsExhausted.Should().BeFalse());
    }

    // ── Test 4.2: AVCO — cálculo de costo promedio ponderado ─────────────

    [Fact]
    public async Task AVCO_GetSummary_ReturnsWeightedAverageCost()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var costRepo = scope.ServiceProvider
            .GetRequiredService<ERP.Domain.Modules.Costing.Interfaces.ICostLayerRepository>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "AVCO-001", ShortName: "AVCO test", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        // Layer 1: 100 units @ $10 = $1,000
        // Layer 2: 50 units @ $20  = $1,000
        // Total: 150 units, $2,000 → AVCO = $13.33...
        db.CostLayers.AddRange(
            ItemsTestSeedData.CreateCostLayer(seed.SubscriberId, seed.CompanyId,
                item.Id, seed.WarehouseId, 100m, 10.00m, seed.UserId),
            ItemsTestSeedData.CreateCostLayer(seed.SubscriberId, seed.CompanyId,
                item.Id, seed.WarehouseId, 50m, 20.00m, seed.UserId)
        );
        await db.SaveChangesAsync();

        var (totalQty, totalCost) = await costRepo.GetAvcoSummaryAsync(
            item.Id, seed.WarehouseId, null, CancellationToken.None);

        totalQty.Should().Be(150m);
        totalCost.Should().Be(2000m, "100×$10 + 50×$20 = $2,000");

        var avco = totalQty > 0 ? totalCost / totalQty : 0;
        avco.Should().BeApproximately(13.33m, 0.01m);
    }

    // ── Test 4.3: FIFO consume correcto → capas agotadas en orden ─────────

    [Fact]
    public async Task FIFO_ConsumeAcrossLayers_ExhaustsInOrder()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var costRepo = scope.ServiceProvider
            .GetRequiredService<ERP.Domain.Modules.Costing.Interfaces.ICostLayerRepository>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "FIFO-CONSUME", ShortName: "FIFO consume", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var now = DateTime.UtcNow;
        var l1 = ItemsTestSeedData.CreateCostLayer(
            seed.SubscriberId, seed.CompanyId, item.Id, seed.WarehouseId,
            30m, 5.00m, seed.UserId, now.AddDays(-15));
        var l2 = ItemsTestSeedData.CreateCostLayer(
            seed.SubscriberId, seed.CompanyId, item.Id, seed.WarehouseId,
            40m, 7.00m, seed.UserId, now.AddDays(-8));

        db.CostLayers.AddRange(l1, l2);
        await db.SaveChangesAsync();

        // Simulate FIFO consumption of 50 units (all of l1 + 20 from l2)
        var fifoLayers = await costRepo.GetFifoLayersAsync(item.Id, seed.WarehouseId, null);
        var remaining = 50m;
        decimal cogsTotal = 0;

        foreach (var layer in fifoLayers)
        {
            if (remaining <= 0) break;
            var consume = Math.Min(layer.RemainingQty, remaining);
            layer.Consume(consume);
            cogsTotal += consume * layer.UnitCost;
            remaining -= consume;
        }

        await db.SaveChangesAsync();

        cogsTotal.Should().Be(30 * 5.00m + 20 * 7.00m, "COGS: 30×$5 + 20×$7 = $290");

        // Verify DB state
        var l1Db = await db.CostLayers.AsNoTracking().FirstAsync(c => c.Id == l1.Id);
        l1Db.IsExhausted.Should().BeTrue("First layer fully consumed");
        l1Db.RemainingQty.Should().Be(0m);

        var l2Db = await db.CostLayers.AsNoTracking().FirstAsync(c => c.Id == l2.Id);
        l2Db.IsExhausted.Should().BeFalse("Second layer partially consumed");
        l2Db.RemainingQty.Should().Be(20m, "40 - 20 = 20 remaining");
    }

    // ── Test 4.4: GetLots filtra por estado ───────────────────────────────

    [Fact]
    public async Task GetLots_FilterByStatus_ReturnsOnlyMatchingLots()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "ITEM-LOT-FILTER", ShortName: "Lot filter", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksLot: true
        ), CancellationToken.None)).Value!;

        // Register 2 lots
        await mediator.Send(new RegisterLotCommand(item.Id, seed.WarehouseId, "LOT-A", 50m), CancellationToken.None);
        await mediator.Send(new RegisterLotCommand(item.Id, seed.WarehouseId, "LOT-B", 30m), CancellationToken.None);

        // Manually deplete LOT-A via domain
        var dbLotA = await db.Lots.FirstAsync(l => l.LotNumber == "LOT-A");
        dbLotA.ConsumeQty(50m); // fully depleted
        await db.SaveChangesAsync();

        // Query active lots only
        var activeResult = await mediator.Send(
            new GetLotsQuery(item.Id, Status: LotStatus.Active),
            CancellationToken.None);

        activeResult.IsSuccess.Should().BeTrue();
        activeResult.Value!.Should().HaveCount(1, "Only LOT-B is still active");
        activeResult.Value![0].LotNumber.Should().Be("LOT-B");

        // Query all lots
        var allResult = await mediator.Send(
            new GetLotsQuery(item.Id),
            CancellationToken.None);

        allResult.Value!.Should().HaveCount(2);
    }
}
