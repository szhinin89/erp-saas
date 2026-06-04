using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.ItemImages;
using ERP.Application.Items.UseCases.ItemUnitConversions;
using ERP.Application.Items.UseCases.ItemSubstitutes;
using ERP.Application.Items.UseCases.ItemPackagingLevels;
using ERP.Application.Items.UseCases.GetItemReport;
using ERP.Application.Items.UseCases.GetStockByItem;
using ERP.Application.Items.UseCases.GetStockKardex;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 13: Módulo Items completo — valida todas las funcionalidades adicionales
/// necesarias para el cierre formal del módulo.
/// </summary>
public sealed class Caso13_ItemCompleteModule_Tests
{
    private static async Task<Guid> CreateItemAsync(IMediator mediator, string sku = "COMPLETE-001")
    {
        var result = await mediator.Send(new CreateItemCommand(
            SKU: sku,
            ShortName: "Ítem completo",
            Description: "Ítem para validación del cierre del módulo",
            ItemType: ItemType.Physical,
            DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true
        ), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Id;
    }

    // ── Test 13.1: ReplaceImages ──────────────────────────────────────────

    [Fact]
    public async Task ReplaceImages_WithValidImages_PersistsToDatabase()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator);

        var storageId1 = Guid.NewGuid();
        var storageId2 = Guid.NewGuid();

        var cmd = new ReplaceItemImagesCommand(itemId, new List<ImageInput>
        {
            new(storageId1, "Foto principal", IsMain: true, IsEcommerce: true, SortOrder: 0),
            new(storageId2, "Foto secundaria", IsMain: false, IsEcommerce: false, SortOrder: 1),
        });

        var result = await mediator.Send(cmd, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Images.Should().HaveCount(2);
        result.Value.Images.Should().ContainSingle(i => i.IsMain);
        result.Value.Images.Should().ContainSingle(i => i.StorageObjectId == storageId1);

        // Verify in DB
        var dbImages = await db.ItemImages
            .Where(i => i.ItemId == itemId && i.IsActive)
            .ToListAsync();
        dbImages.Should().HaveCount(2);
    }

    // ── Test 13.2: Two main images rejected by validator ─────────────────

    [Fact]
    public async Task ReplaceImages_TwoMainImages_ThrowsValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "IMG-DUP-MAIN");

        // ValidationBehavior throws FluentValidation.ValidationException for validator failures
        var act = async () => await mediator.Send(new ReplaceItemImagesCommand(itemId, new List<ImageInput>
        {
            new(Guid.NewGuid(), IsMain: true),
            new(Guid.NewGuid(), IsMain: true),
        }), CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>(
            "Two main images must be rejected by the FluentValidation pipeline");
    }

    // ── Test 13.3: ReplaceUnitConversions ────────────────────────────────

    [Fact]
    public async Task ReplaceUnitConversions_WithValidConversions_Persists()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "CONV-001");

        var cmd = new ReplaceItemUnitConversionsCommand(itemId, new List<UnitConversionInput>
        {
            new("UND", "CJA", 24m),
            new("UND", "KG", 0.450m),
        });

        var result = await mediator.Send(cmd, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.UnitConversions.Should().HaveCount(2);

        var cja = result.Value.UnitConversions.FirstOrDefault(c => c.ToUomCode == "CJA");
        cja.Should().NotBeNull();
        cja!.Factor.Should().Be(24m);
    }

    // ── Test 13.4: ReplaceSubstitutes ────────────────────────────────────

    [Fact]
    public async Task ReplaceSubstitutes_WithValidItems_Persists()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId  = await CreateItemAsync(mediator, "ORIG-001");
        var subId   = await CreateItemAsync(mediator, "SUB-001");

        var cmd = new ReplaceItemSubstitutesCommand(itemId, new List<SubstituteInput>
        {
            new(subId, Priority: 1, Note: "Alternativa preferida"),
        });

        var result = await mediator.Send(cmd, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Substitutes.Should().HaveCount(1);
        result.Value.Substitutes[0].SubstituteItemId.Should().Be(subId);
        result.Value.Substitutes[0].Priority.Should().Be(1);
    }

    // ── Test 13.5: Self-substitute rejected ──────────────────────────────

    [Fact]
    public async Task ReplaceSubstitutes_SelfReference_ReturnsValidationFailure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "SELF-SUB");

        var result = await mediator.Send(new ReplaceItemSubstitutesCommand(itemId,
            new List<SubstituteInput> { new(itemId, 1) }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse("Self-substitution must be rejected");
    }

    // ── Test 13.6: ReplacePackagingLevels ───────────────────────────────

    [Fact]
    public async Task ReplacePackagingLevels_WithBaseUnit_Persists()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "PKG-001");

        var cmd = new ReplaceItemPackagingLevelsCommand(itemId, new List<PackagingLevelInput>
        {
            new("Unidad", Level: 0, BaseQuantity: 1m, UomCode: "UND", IsBaseUnit: true, IsSaleDefault: true),
            new("Caja",   Level: 1, BaseQuantity: 24m, UomCode: "CJA", IsPurchaseDefault: true),
            new("Bulto",  Level: 2, BaseQuantity: 288m, UomCode: "BLT"),
        });

        var result = await mediator.Send(cmd, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.PackagingLevels.Should().HaveCount(3);
        result.Value.PackagingLevels.Should().ContainSingle(p => p.IsBaseUnit);
        result.Value.PackagingLevels.Should().ContainSingle(p => p.UomCode == "CJA" && p.BaseQuantity == 24m);
    }

    // ── Test 13.7: Packaging without base unit rejected by validator ─────

    [Fact]
    public async Task ReplacePackagingLevels_NoBaseUnit_ThrowsValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "PKG-NO-BASE");

        var act = async () => await mediator.Send(new ReplaceItemPackagingLevelsCommand(itemId,
            new List<PackagingLevelInput>
            {
                new("Caja", 1, 24m, "CJA"),
                new("Bulto", 2, 288m, "BLT"),
            }), CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>(
            "Missing base unit must be rejected by FluentValidation pipeline");
    }

    // ── Test 13.8: GetItemReport — paginated ─────────────────────────────

    [Fact]
    public async Task GetItemReport_Paginated_ReturnsCorrectPage()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        // Create 5 items
        for (var i = 1; i <= 5; i++)
            await CreateItemAsync(mediator, $"RPT-{i:D3}");

        // Page 1 of 2 (pageSize=3)
        var p1 = await mediator.Send(new GetItemReportQuery(PageNumber: 1, PageSize: 3), CancellationToken.None);
        p1.IsSuccess.Should().BeTrue();
        p1.Value!.Items.Should().HaveCount(3);
        p1.Value.TotalCount.Should().Be(5);
        p1.Value.PageSize.Should().Be(3);

        // Page 2
        var p2 = await mediator.Send(new GetItemReportQuery(PageNumber: 2, PageSize: 3), CancellationToken.None);
        p2.Value!.Items.Should().HaveCount(2);
    }

    // ── Test 13.9: GetItemFullReport — returns brand name ────────────────

    [Fact]
    public async Task GetItemFullReport_ReturnsBrandName_WhenBrandAssigned()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "FULL-RPT-001");

        var result = await mediator.Send(new GetItemFullReportQuery(itemId), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SKU.Should().Be("FULL-RPT-001");
        result.Value.Variants.Should().NotBeNull();
        result.Value.Images.Should().NotBeNull();
        result.Value.UnitConversions.Should().NotBeNull();
        result.Value.Substitutes.Should().NotBeNull();
        result.Value.PackagingLevels.Should().NotBeNull();
    }

    // ── Test 13.10: GetStockByItem — returns empty for new item ──────────

    [Fact]
    public async Task GetStockByItem_NewItem_ReturnsEmptyList()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "STOCK-NEW-001");

        var result = await mediator.Send(new GetStockByItemQuery(itemId), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Should().BeEmpty("New item has no stock entries");
    }

    // ── Test 13.11: GetStockKardex — returns empty for new item ─────────

    [Fact]
    public async Task GetStockKardex_NewItem_ReturnsEmptyKardex()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateItemAsync(mediator, "KARDEX-NEW-001");

        var result = await mediator.Send(new GetStockKardexQuery(itemId), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().BeEmpty("New item has no movements");
        result.Value.TotalCount.Should().Be(0);
    }
}
