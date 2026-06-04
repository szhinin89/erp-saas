using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.AddItemVariant;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.GetItemById;
using ERP.Application.Items.UseCases.DisableItemVariant;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 2: Crear ítem con variantes.
/// Cubre: matriz de variantes (Color × Talla), SKU auto-generado,
/// unicidad de combinación de atributos, disable de variante.
/// </summary>
public sealed class Caso2_CreateItem_WithVariants_Tests
{
    private static async Task<(Guid itemId, Guid colorDefId, Guid sizeDefId)> SetupAsync(
        ErpDbContext db, IMediator mediator, IntegrationTestWebAppFactory factory)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var tid    = seed.SubscriberId;
        var userId = seed.UserId;

        // Create attribute groups and definitions
        var (group, colorDef, sizeDef) = ItemsTestSeedData.CreateVariantAxes(tid, userId);
        db.AttributeGroups.Add(group);
        db.AttributeDefinitions.AddRange(colorDef, sizeDef);
        await db.SaveChangesAsync();

        // Create item
        var itemResult = await mediator.Send(new CreateItemCommand(
            SKU: "CAMISA-001",
            ShortName: "Camisa de algodón",
            Description: "Camisa de algodón 100% para hombre",
            ItemType: ItemType.Physical,
            DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true
        ), CancellationToken.None);

        itemResult.IsSuccess.Should().BeTrue(itemResult.Error);

        return (itemResult.Value!.Id, colorDef.Id, sizeDef.Id);
    }

    // ── Test 2.1: Agregar variante con atributos ──────────────────────────

    [Fact]
    public async Task AddVariant_WithColorAndSize_CreatesVariantWithAttributes()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (itemId, colorDefId, sizeDefId) = await SetupAsync(db, mediator, factory);

        var command = new AddItemVariantCommand(
            ItemId: itemId,
            Attributes: new List<VariantAttributeInput>
            {
                new(colorDefId, "Rojo"),
                new(sizeDefId, "M"),
            }
        );

        var result = await mediator.Send(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Name.Should().Contain("Rojo");
        result.Value.Name.Should().Contain("M");
        result.Value.IsDefault.Should().BeTrue("First variant is default");
        result.Value.IsActive.Should().BeTrue();
        result.Value.Attributes.Should().HaveCount(2);
    }

    // ── Test 2.2: Matriz completa Color × Talla ───────────────────────────

    [Fact]
    public async Task AddVariants_ColorTimesSize_Matrix_CreatesAllCombinations()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (itemId, colorDefId, sizeDefId) = await SetupAsync(db, mediator, factory);

        // Color axes: Rojo, Azul | Size axes: S, M, L → 6 combinations
        var colors = new[] { "Rojo", "Azul" };
        var sizes  = new[] { "S", "M", "L" };

        var createdVariants = new List<Guid>();
        foreach (var color in colors)
        {
            foreach (var size in sizes)
            {
                var cmd = new AddItemVariantCommand(
                    ItemId: itemId,
                    Attributes: new List<VariantAttributeInput>
                    {
                        new(colorDefId, color),
                        new(sizeDefId, size),
                    }
                );
                var r = await mediator.Send(cmd, CancellationToken.None);
                r.IsSuccess.Should().BeTrue($"Variant {color}/{size} should be created");
                createdVariants.Add(r.Value!.Id);
            }
        }

        // Verify 6 variants in DB
        var item = await db.Items
            .Include(x => x.Variants)
                .ThenInclude(v => v.Attributes)
            .FirstAsync(x => x.Id == itemId);

        item.Variants.Should().HaveCount(6, "2 colors × 3 sizes = 6 combinations");
        item.Variants.Where(v => v.IsActive).Should().HaveCount(6);
        item.Variants.Where(v => v.IsDefault).Should().HaveCount(1, "Exactly one default variant");

        // Verify SKUs are unique and auto-generated
        var skus = item.Variants.Select(v => v.SKU).ToList();
        skus.Distinct().Should().HaveCount(6, "All SKUs must be unique");
        skus.Should().AllSatisfy(sku => sku.Should().StartWith("CAMISA-001-"));
    }

    // ── Test 2.3: Variante duplicada es rechazada ─────────────────────────

    [Fact]
    public async Task AddVariant_DuplicateAttributeCombination_ReturnsConflict()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (itemId, colorDefId, sizeDefId) = await SetupAsync(db, mediator, factory);

        var attrs = new List<VariantAttributeInput>
        {
            new(colorDefId, "Verde"),
            new(sizeDefId, "L"),
        };

        // Create first
        var first = await mediator.Send(new AddItemVariantCommand(itemId, attrs), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Try to create duplicate
        var dup = await mediator.Send(new AddItemVariantCommand(itemId, attrs), CancellationToken.None);
        dup.IsSuccess.Should().BeFalse("Duplicate attribute combination must be rejected");
    }

    // ── Test 2.4: SKU override personalizado ─────────────────────────────

    [Fact]
    public async Task AddVariant_WithCustomSKU_UsesOverride()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (itemId, colorDefId, sizeDefId) = await SetupAsync(db, mediator, factory);

        var command = new AddItemVariantCommand(
            ItemId: itemId,
            Attributes: new List<VariantAttributeInput> { new(colorDefId, "Negro") },
            SkuOverride: "CAMISA-NEGRA-CUSTOM"
        );

        var result = await mediator.Send(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SKU.Should().Be("CAMISA-NEGRA-CUSTOM");
    }

    // ── Test 2.5: GetItemById retorna variantes con atributos ─────────────

    [Fact]
    public async Task GetItemById_WithVariants_ReturnsFullDetail()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (itemId, colorDefId, sizeDefId) = await SetupAsync(db, mediator, factory);

        await mediator.Send(new AddItemVariantCommand(itemId,
            new List<VariantAttributeInput> { new(colorDefId, "Blanco"), new(sizeDefId, "S") }
        ), CancellationToken.None);

        await mediator.Send(new AddItemVariantCommand(itemId,
            new List<VariantAttributeInput> { new(colorDefId, "Blanco"), new(sizeDefId, "M") }
        ), CancellationToken.None);

        var detail = await mediator.Send(new GetItemByIdQuery(itemId), CancellationToken.None);

        detail.IsSuccess.Should().BeTrue();
        detail.Value!.Variants.Should().HaveCount(2);
        detail.Value.Variants[0].Attributes.Should().HaveCount(2);
        detail.Value.Variants.Should().ContainSingle(v => v.IsDefault);
    }

    // ── Test 2.6: Deshabilitar variante ───────────────────────────────────

    [Fact]
    public async Task DisableVariant_SetsIsActiveFalse()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (itemId, colorDefId, sizeDefId) = await SetupAsync(db, mediator, factory);

        var variant = (await mediator.Send(new AddItemVariantCommand(itemId,
            new List<VariantAttributeInput> { new(colorDefId, "Gris") }
        ), CancellationToken.None)).Value!;

        var disableResult = await mediator.Send(
            new DisableItemVariantCommand(itemId, variant.Id),
            CancellationToken.None);

        disableResult.IsSuccess.Should().BeTrue(disableResult.Error);

        var dbVariant = await db.ItemVariants.AsNoTracking()
            .FirstAsync(v => v.Id == variant.Id);
        dbVariant.IsActive.Should().BeFalse();
    }
}
