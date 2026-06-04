using ERP.API.Tests.Support;
using ERP.Application.DigitalItems.UseCases.AddDeliverable;
using ERP.Application.DigitalItems.UseCases.CreateDigitalItem;
using ERP.Application.DigitalItems.UseCases.GetDigitalItem;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Domain.Modules.DigitalItems.Enums;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 6: Producto Digital.
/// Cubre: crear ítem digital, configurar licencia/entrega, agregar descargables,
/// e invariantes (solo ítems tipo Digital, un único DigitalItem por Item).
/// </summary>
public sealed class Caso6_DigitalProduct_Tests
{
    private static readonly Guid TestStorageObjectId = Guid.NewGuid();

    private static async Task<Guid> CreateDigitalItemAsync(IMediator mediator)
    {
        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "SW-ERP-PROF",
            ShortName: "ERP Professional",
            Description: "Software ERP para PyMEs — licencia perpetua",
            ItemType: ItemType.Digital,
            DefaultUomCode: "LIC",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: false
        ), CancellationToken.None)).Value!;

        return item.Id;
    }

    // ── Test 6.1: Crear ítem digital con licencia perpetua ────────────────

    [Fact]
    public async Task CreateDigitalItem_Perpetual_Persists()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateDigitalItemAsync(mediator);

        var diCmd = new CreateDigitalItemCommand(
            ItemId:         itemId,
            LicenseType:    LicenseType.Perpetual,
            DeliveryMethod: DeliveryMethod.Download,
            MaxDownloads:   3
        );

        var result = await mediator.Send(diCmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LicenseType.Should().Be("Perpetual");
        result.Value.DeliveryMethod.Should().Be("Download");
        result.Value.MaxDownloads.Should().Be(3);
        result.Value.ValidityDays.Should().BeNull("Perpetual license has no expiry");

        // Verify DB
        var dbDi = await db.DigitalItems.FirstOrDefaultAsync(x => x.Id == result.Value.Id);
        dbDi.Should().NotBeNull();
        dbDi!.ItemId.Should().Be(itemId);
    }

    // ── Test 6.2: Ítem tipo Physical no puede ser Digital ─────────────────

    [Fact]
    public async Task CreateDigitalItem_ForPhysicalItem_ReturnsValidationFailure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        // Physical item (wrong type)
        var physItem = (await mediator.Send(new CreateItemCommand(
            SKU: "PHYS-ITEM", ShortName: "Physical", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var result = await mediator.Send(new CreateDigitalItemCommand(
            physItem.Id, LicenseType.Perpetual, DeliveryMethod.Download
        ), CancellationToken.None);

        result.IsSuccess.Should().BeFalse("Physical item cannot have digital config");
        result.Error.Should().ContainEquivalentOf("digital");
    }

    // ── Test 6.3: Ítem duplicado de DigitalItem es rechazado ─────────────

    [Fact]
    public async Task CreateDigitalItem_DuplicateForSameItem_ReturnsConflict()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateDigitalItemAsync(mediator);

        await mediator.Send(new CreateDigitalItemCommand(
            itemId, LicenseType.Perpetual, DeliveryMethod.Download), CancellationToken.None);

        var dup = await mediator.Send(new CreateDigitalItemCommand(
            itemId, LicenseType.Subscription, DeliveryMethod.Email), CancellationToken.None);

        dup.IsSuccess.Should().BeFalse("Only one DigitalItem per Item allowed");
    }

    // ── Test 6.4: Agregar descargable ────────────────────────────────────

    [Fact]
    public async Task AddDeliverable_ToDigitalItem_PersistsFile()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var itemId = await CreateDigitalItemAsync(mediator);

        var di = (await mediator.Send(new CreateDigitalItemCommand(
            itemId, LicenseType.Perpetual, DeliveryMethod.Download, MaxDownloads: 5
        ), CancellationToken.None)).Value!;

        // Add installer file
        var d1 = await mediator.Send(new AddDeliverableCommand(
            DigitalItemId:   di.Id,
            Name:            "ERP Professional v3.0 Setup",
            StorageObjectId: TestStorageObjectId,
            Version:         "3.0.1"
        ), CancellationToken.None);

        d1.IsSuccess.Should().BeTrue(d1.Error);
        d1.Value!.Name.Should().Be("ERP Professional v3.0 Setup");
        d1.Value.Version.Should().Be("3.0.1");
        d1.Value.StorageObjectId.Should().Be(TestStorageObjectId);
        d1.Value.IsActive.Should().BeTrue();

        // Add documentation
        await mediator.Send(new AddDeliverableCommand(
            DigitalItemId:   di.Id,
            Name:            "Manual de usuario PDF",
            StorageObjectId: Guid.NewGuid()
        ), CancellationToken.None);

        // Verify GetDigitalItemByItemId returns both deliverables
        var getResult = await mediator.Send(
            new GetDigitalItemByItemIdQuery(itemId),
            CancellationToken.None);

        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.Deliverables.Should().HaveCount(2);
        getResult.Value.Deliverables.Should().AllSatisfy(d => d.IsActive.Should().BeTrue());
    }

    // ── Test 6.5: Ítem Digital con suscripción y validez en días ─────────

    [Fact]
    public async Task CreateDigitalItem_Subscription_WithValidityDays()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "SW-SUBS-ANNUAL",
            ShortName: "ERP Anual",
            Description: "Suscripción anual",
            ItemType: ItemType.Digital,
            DefaultUomCode: "LIC",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var result = await mediator.Send(new CreateDigitalItemCommand(
            ItemId:         item.Id,
            LicenseType:    LicenseType.Subscription,
            DeliveryMethod: DeliveryMethod.Api,
            ValidityDays:   365
        ), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ValidityDays.Should().Be(365);
        result.Value.LicenseType.Should().Be("Subscription");
        result.Value.DeliveryMethod.Should().Be("Api");
    }

    // ── Test 6.6: GetDigitalItem — ítem no configurado retorna NotFound ───

    [Fact]
    public async Task GetDigitalItem_ForItemWithoutConfig_ReturnsNotFound()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "DIG-NO-CFG", ShortName: "No config", Description: "Test",
            ItemType: ItemType.Digital, DefaultUomCode: "LIC",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var result = await mediator.Send(
            new GetDigitalItemByItemIdQuery(item.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("not_found");
    }
}
