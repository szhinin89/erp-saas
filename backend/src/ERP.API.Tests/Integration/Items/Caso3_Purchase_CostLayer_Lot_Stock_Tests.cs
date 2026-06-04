using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.RegisterLot;
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
/// Caso 3: Comprar producto → CostLayer + Lot + Stock.
///
/// Estado de implementación:
/// ✅ RegisterLotCommand — completamente implementado
/// ✅ CostLayer domain entity — completamente implementado
/// ⚠️  PurchaseReceipt BC (Application layer) — pendiente de implementar.
///     Por ahora se valida:
///     a) RegisterLot directamente
///     b) Creación de CostLayer vía dominio (simula lo que PurchaseReceipt.Confirm() haría)
///     c) Invariantes de negocio: Lot.ConsumeQty(), Lot.RegisterEntry()
/// </summary>
public sealed class Caso3_Purchase_CostLayer_Lot_Stock_Tests
{
    // ── Test 3.1: Registrar lote para ítem con TracksLot=true ─────────────

    [Fact]
    public async Task RegisterLot_ForLotTrackedItem_CreatesLotInDatabase()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        // Create item with lot tracking
        var itemResult = await mediator.Send(new CreateItemCommand(
            SKU: "ACEITE-LOT-001",
            ShortName: "Aceite con lote",
            Description: "Aceite de girasol con trazabilidad de lote",
            ItemType: ItemType.Physical,
            DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true,
            TracksLot: true
        ), CancellationToken.None);

        itemResult.IsSuccess.Should().BeTrue(itemResult.Error);
        var itemId = itemResult.Value!.Id;

        // Register lot
        var lotCmd = new RegisterLotCommand(
            ItemId:          itemId,
            WarehouseId:     seed.WarehouseId,
            LotNumber:       "LOT-2026-001",
            InitialQty:      100m,
            ManufactureDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            ExpirationDate:  DateOnly.FromDateTime(DateTime.Today.AddDays(365)),
            Notes:           "Lote de prueba integración"
        );

        var lotResult = await mediator.Send(lotCmd, CancellationToken.None);

        lotResult.IsSuccess.Should().BeTrue(lotResult.Error);
        lotResult.Value!.LotNumber.Should().Be("LOT-2026-001");
        lotResult.Value.InitialQty.Should().Be(100m);
        lotResult.Value.CurrentQty.Should().Be(100m);
        lotResult.Value.Status.Should().Be("Active");

        // Verify in DB
        var dbLot = await db.Lots.FirstOrDefaultAsync(l => l.Id == lotResult.Value.Id);
        dbLot.Should().NotBeNull();
        dbLot!.ItemId.Should().Be(itemId);
        dbLot.LotNumber.Should().Be("LOT-2026-001");
        dbLot.ExpirationDate.Should().NotBeNull();
    }

    // ── Test 3.2: No se puede registrar lote si TracksLot=false ──────────

    [Fact]
    public async Task RegisterLot_ForNonLotTrackedItem_ReturnsValidationFailure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        // Item sin trazabilidad de lote
        var itemResult = await mediator.Send(new CreateItemCommand(
            SKU: "ITEM-NO-LOT",
            ShortName: "Ítem sin lote",
            Description: "No trackea lote",
            ItemType: ItemType.Physical,
            DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true,
            TracksLot: false
        ), CancellationToken.None);

        var result = await mediator.Send(new RegisterLotCommand(
            ItemId: itemResult.Value!.Id,
            WarehouseId: seed.WarehouseId,
            LotNumber: "LOT-X",
            InitialQty: 50m
        ), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().ContainEquivalentOf("lote");
    }

    // ── Test 3.3: Número de lote duplicado por ítem es rechazado ─────────

    [Fact]
    public async Task RegisterLot_DuplicateLotNumber_ReturnsConflict()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "ITEM-DUPLOT", ShortName: "Dup lot", Description: "Dup lot test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksLot: true
        ), CancellationToken.None)).Value!;

        await mediator.Send(new RegisterLotCommand(item.Id, seed.WarehouseId, "LOT-DUP", 50m), CancellationToken.None);
        var dup = await mediator.Send(new RegisterLotCommand(item.Id, seed.WarehouseId, "LOT-DUP", 30m), CancellationToken.None);

        dup.IsSuccess.Should().BeFalse("Duplicate lot number for same item must be rejected");
    }

    // ── Test 3.4: CostLayer domain invariants ─────────────────────────────

    [Fact]
    public async Task CostLayer_Domain_ConsumeQty_ReducesRemainingQty_ExhaustsAtZero()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "ITEM-COST", ShortName: "Item costos", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        // Create CostLayer via domain (simulating what PurchaseReceipt.Confirm() does)
        var layer = ItemsTestSeedData.CreateCostLayer(
            seed.SubscriberId, seed.CompanyId,
            item.Id, seed.WarehouseId,
            entryQty: 50m, unitCost: 10.00m,
            createdBy: seed.UserId,
            layerDate: DateTime.UtcNow.AddDays(-5));

        db.CostLayers.Add(layer);
        await db.SaveChangesAsync();

        // Verify initial state
        layer.RemainingQty.Should().Be(50m);
        layer.IsExhausted.Should().BeFalse();
        layer.TotalCost.Should().Be(500m);  // 50 × 10

        // Consume 30 units
        layer.Consume(30m);
        layer.RemainingQty.Should().Be(20m);
        layer.IsExhausted.Should().BeFalse();

        // Consume remaining 20 units → should be exhausted
        layer.Consume(20m);
        layer.RemainingQty.Should().Be(0m);
        layer.IsExhausted.Should().BeTrue("Layer should be exhausted when RemainingQty = 0");

        // Attempt to consume from exhausted layer → exception
        var act = () => layer.Consume(1m);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Test 3.5: Múltiples capas — escenario FIFO en base de datos ───────

    [Fact]
    public async Task CostLayers_MultipleLayers_FifoOrder_OldestFirstInQuery()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "ITEM-FIFO", ShortName: "Item FIFO", Description: "Test FIFO",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var now = DateTime.UtcNow;

        // Create 3 layers at different dates with different costs
        var layer1 = ItemsTestSeedData.CreateCostLayer(
            seed.SubscriberId, seed.CompanyId, item.Id, seed.WarehouseId,
            50m, 8.00m, seed.UserId, now.AddDays(-10));  // oldest: cheapest

        var layer2 = ItemsTestSeedData.CreateCostLayer(
            seed.SubscriberId, seed.CompanyId, item.Id, seed.WarehouseId,
            30m, 10.00m, seed.UserId, now.AddDays(-5));

        var layer3 = ItemsTestSeedData.CreateCostLayer(
            seed.SubscriberId, seed.CompanyId, item.Id, seed.WarehouseId,
            20m, 12.00m, seed.UserId, now.AddDays(-1));  // newest: most expensive

        db.CostLayers.AddRange(layer1, layer2, layer3);
        await db.SaveChangesAsync();

        // Query FIFO order (OrderBy LayerDate ASC)
        var fifoLayers = await db.CostLayers
            .Where(c =>
                c.SubscriberId == seed.SubscriberId &&
                c.CompanyId == seed.CompanyId &&
                c.ItemId == item.Id &&
                c.WarehouseId == seed.WarehouseId &&
                !c.IsExhausted)
            .OrderBy(c => c.LayerDate)
            .ToListAsync();

        fifoLayers.Should().HaveCount(3);
        fifoLayers[0].UnitCost.Should().Be(8.00m, "FIFO: oldest (cheapest) layer should be first");
        fifoLayers[1].UnitCost.Should().Be(10.00m);
        fifoLayers[2].UnitCost.Should().Be(12.00m, "FIFO: newest (most expensive) layer should be last");

        // Simulate consuming 60 units in FIFO order
        // Layer1 (50 units @ $8): consume all 50
        // Layer2 (30 units @ $10): consume 10
        var toConsume = 60m;
        decimal totalCost = 0;
        foreach (var layer in fifoLayers)
        {
            if (toConsume <= 0) break;
            var consume = Math.Min(layer.RemainingQty, toConsume);
            totalCost += consume * layer.UnitCost;
            layer.Consume(consume);
            toConsume -= consume;
        }

        totalCost.Should().Be(50 * 8.00m + 10 * 10.00m, "FIFO cost: 50×$8 + 10×$10 = $500");
        fifoLayers[0].IsExhausted.Should().BeTrue("First layer should be exhausted");
        fifoLayers[1].RemainingQty.Should().Be(20m, "Second layer: 30 - 10 = 20 remaining");
    }

    // ── Test 3.6: Lot.ConsumeQty invariants ──────────────────────────────

    [Fact]
    public async Task Lot_ConsumeQty_BelowZero_ThrowsException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "ITEM-LOT-INV", ShortName: "Lot invariant", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksLot: true
        ), CancellationToken.None)).Value!;

        var lotResult = await mediator.Send(new RegisterLotCommand(
            item.Id, seed.WarehouseId, "LOT-INV-001", 25m), CancellationToken.None);

        lotResult.IsSuccess.Should().BeTrue();

        // Load lot and verify invariants
        var dbLot = await db.Lots.FirstAsync(l => l.Id == lotResult.Value!.Id);

        // Consume more than available → exception
        var act = () => dbLot.ConsumeQty(100m);
        act.Should().Throw<InvalidOperationException>();

        // Consume exact amount → depleted
        dbLot.ConsumeQty(25m);
        dbLot.Status.Should().Be(LotStatus.Depleted);
        dbLot.CurrentQty.Should().Be(0m);

        // Try to consume from depleted lot → exception
        var act2 = () => dbLot.ConsumeQty(1m);
        act2.Should().Throw<InvalidOperationException>();
    }
}
