using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.UpdateItem;
using ERP.Application.Items.UseCases.DisableItem;
using ERP.Application.Items.UseCases.EnableItem;
using ERP.Application.Items.UseCases.GetItemById;
using ERP.Application.Items.UseCases.GetItems;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 1: Crear ítem simple.
/// Cubre: creación, lectura, actualización, deshabilitación, reactivación.
/// Verifica invariantes de negocio: SKU único, validación de campos.
/// </summary>
public sealed class Caso1_CreateItem_Simple_Tests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static CreateItemCommand BuildCreateCommand(string sku = "SKU-CASE1-001") =>
        new(
            SKU:            sku,
            ShortName:      "Aceite Girasol 1L",
            Description:    "Aceite de girasol refinado 1 litro",
            ItemType:       ItemType.Physical,
            DefaultUomCode: "UND",
            AppliesVatOnSale:      false,
            SaleVatCode:           null,
            VatAccountId:          null,
            AppliesVatOnPurchase:  false,
            PurchaseVatCode:       null,
            PurchaseVatAccountId:  null,
            PurchaseCode:          "PROV-ACS-001",
            Observations:          "Uso en línea de alimentos",
            IsForSale:             true,
            TracksStock:           true,
            TracksLot:             true
        );

    // ── Test 1.1: Item se crea correctamente ──────────────────────────────

    [Fact]
    public async Task CreateItem_WithValidData_PersistsToDatabase()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var command = BuildCreateCommand();
        var result  = await mediator.Send(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().NotBeNull();
        result.Value!.SKU.Should().Be("SKU-CASE1-001");
        result.Value.ItemType.Should().Be("Physical");
        result.Value.IsActive.Should().BeTrue();
        result.Value.TracksLot.Should().BeTrue();

        // Verify DB persistence
        var dbItem = await db.Items
            .FirstOrDefaultAsync(x => x.Id == result.Value.Id);

        dbItem.Should().NotBeNull();
        dbItem!.Code.SKU.Should().Be("SKU-CASE1-001");
        dbItem.Code.PurchaseCode.Should().Be("PROV-ACS-001");
        dbItem.DefaultUomCode.Should().Be("UND");
        dbItem.StockConfig.TracksLot.Should().BeTrue();
        dbItem.IsActive.Should().BeTrue();
    }

    // ── Test 1.2: SKU único por subscriber ────────────────────────────────

    [Fact]
    public async Task CreateItem_DuplicateSKU_ReturnsConflict()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        // Create first item
        var first  = await mediator.Send(BuildCreateCommand("SKU-DUP-001"), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Try to create another with same SKU
        var second = await mediator.Send(BuildCreateCommand("SKU-DUP-001"), CancellationToken.None);
        second.IsSuccess.Should().BeFalse();
        second.ErrorCode.Should().Be("SKU_DUPLICATE");
    }

    // ── Test 1.3: Actualización de campos (SKU inmutable) ─────────────────

    [Fact]
    public async Task UpdateItem_ChangesScalarFields_SKURemainsImmutable()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var created = (await mediator.Send(BuildCreateCommand("SKU-UPD-001"), CancellationToken.None)).Value!;

        var updateCmd = new UpdateItemCommand(
            Id:           created.Id,
            ShortName:    "Aceite Girasol Actualizado",
            Description:  "Nueva descripción",
            DefaultUomCode: "CJA",
            AppliesVatOnSale: false,
            SaleVatCode: null,
            VatAccountId: null,
            AppliesVatOnPurchase: false,
            PurchaseVatCode: null,
            PurchaseVatAccountId: null,
            IsForSale: true,
            TracksStock: true
        );

        var updateResult = await mediator.Send(updateCmd, CancellationToken.None);
        updateResult.IsSuccess.Should().BeTrue(updateResult.Error);

        // Verify in DB
        var dbItem = await db.Items.AsNoTracking()
            .FirstAsync(x => x.Id == created.Id);

        dbItem.Code.SKU.Should().Be("SKU-UPD-001");           // SKU unchanged
        dbItem.Code.ShortName.Should().Be("Aceite Girasol Actualizado");
        dbItem.Code.Description.Should().Be("Nueva descripción");
        dbItem.DefaultUomCode.Should().Be("CJA");
    }

    // ── Test 1.4: Deshabilitar ítem ───────────────────────────────────────

    [Fact]
    public async Task DisableItem_SetsIsActiveFalse_CanBeReenabled()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var created = (await mediator.Send(BuildCreateCommand("SKU-DIS-001"), CancellationToken.None)).Value!;

        // Disable
        var disableResult = await mediator.Send(new DisableItemCommand(created.Id), CancellationToken.None);
        disableResult.IsSuccess.Should().BeTrue(disableResult.Error);

        var disabled = await db.Items.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        disabled.IsActive.Should().BeFalse();

        // Re-enable
        var enableResult = await mediator.Send(new EnableItemCommand(created.Id), CancellationToken.None);
        enableResult.IsSuccess.Should().BeTrue(enableResult.Error);

        var enabled = await db.Items.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        enabled.IsActive.Should().BeTrue();
    }

    // ── Test 1.5: Consulta paginada con filtros ───────────────────────────

    [Fact]
    public async Task GetItems_FilterBySKUAndType_ReturnsMatchingItems()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        // Create 3 items
        await mediator.Send(BuildCreateCommand("ACEITE-001"), CancellationToken.None);
        await mediator.Send(BuildCreateCommand("ACEITE-002"), CancellationToken.None);
        await mediator.Send(new CreateItemCommand(
            SKU: "SVC-LIMPIEZA",
            ShortName: "Servicio limpieza",
            Description: "Desc",
            ItemType: ItemType.Service,
            DefaultUomCode: "HRS",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None);

        // Filter by type
        var physicalResult = await mediator.Send(
            new GetItemsQuery(ItemType: ItemType.Physical, PageSize: 50),
            CancellationToken.None);

        physicalResult.IsSuccess.Should().BeTrue();
        physicalResult.Value!.Items.Should().HaveCount(2);
        physicalResult.Value.Items.Should().AllSatisfy(i => i.ItemType.Should().Be("Physical"));

        // Filter by SKU search
        var searchResult = await mediator.Send(
            new GetItemsQuery(Search: "ACEITE", PageSize: 50),
            CancellationToken.None);

        searchResult.Value!.Items.Should().HaveCount(2);

        // GetById
        var firstItem = physicalResult.Value.Items.First();
        var detailResult = await mediator.Send(
            new GetItemByIdQuery(firstItem.Id),
            CancellationToken.None);

        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value!.SKU.Should().Be(firstItem.SKU);
    }

    // ── Test 1.6: Domain Events se registran en OutboxMessages ───────────

    [Fact]
    public async Task CreateItem_RaisesItemCreatedEvent_StoredInOutbox()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var result = await mediator.Send(BuildCreateCommand("SKU-EVT-001"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        // OutboxMessages should contain ItemCreatedEvent
        var outbox = await db.OutboxMessages
            .Where(m => m.EventName.Contains("ItemCreated"))
            .ToListAsync();

        outbox.Count.Should().BeGreaterThan(0,
            "CreateItem should raise ItemCreatedEvent stored in outbox");
    }
}
