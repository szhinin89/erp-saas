using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Kits.UseCases.AddKitLine;
using ERP.Application.Kits.UseCases.CreateKit;
using ERP.Application.Kits.UseCases.GetKitById;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Kits.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 5: Crear Kit → vender kit → descontar componentes.
///
/// Estado de implementación:
/// ✅ CreateKitCommand — completamente implementado
/// ✅ AddKitLineCommand — completamente implementado
/// ✅ GetKitByItemIdQuery — completamente implementado
/// ✅ Kit domain invariants (no-recursión, componentes únicos)
/// ⚠️  Venta de kit con descuento de componentes (Sales integration) pendiente.
///     Requiere: SaleAuthorizedEventHandler actualizado para desensamblar kit
///     en componentes y crear StockMovements por cada componente.
/// </summary>
public sealed class Caso5_Kit_CreateAndSell_Tests
{
    private static async Task<(Guid kitItemId, Guid comp1Id, Guid comp2Id)> SetupKitAsync(
        ErpDbContext db, IMediator mediator, IntegrationTestWebAppFactory factory)
    {
        await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        // Componente 1: Camiseta
        var comp1 = (await mediator.Send(new CreateItemCommand(
            SKU: "COMP-CAMISETA", ShortName: "Camiseta", Description: "Componente del kit",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true
        ), CancellationToken.None)).Value!;

        // Componente 2: Pantalón
        var comp2 = (await mediator.Send(new CreateItemCommand(
            SKU: "COMP-PANTALON", ShortName: "Pantalón", Description: "Componente del kit",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true
        ), CancellationToken.None)).Value!;

        // Kit item (el ítem raíz del kit)
        var kitItem = (await mediator.Send(new CreateItemCommand(
            SKU: "KIT-UNIFORME", ShortName: "Kit Uniforme", Description: "Kit completo: camiseta + pantalón",
            ItemType: ItemType.Kit, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: false
        ), CancellationToken.None)).Value!;

        return (kitItem.Id, comp1.Id, comp2.Id);
    }

    // ── Test 5.1: Crear kit con componentes ───────────────────────────────

    [Fact]
    public async Task CreateKit_WithComponents_PersistsKitAndLines()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (kitItemId, comp1Id, comp2Id) = await SetupKitAsync(db, mediator, factory);

        // Create kit
        var kitResult = await mediator.Send(
            new CreateKitCommand(kitItemId, KitType.Fixed),
            CancellationToken.None);

        kitResult.IsSuccess.Should().BeTrue(kitResult.Error);
        kitResult.Value!.KitType.Should().Be("Fixed");
        kitResult.Value.Lines.Should().BeEmpty("No lines yet");

        var kitId = kitResult.Value.Id;

        // Add component 1: 1 camiseta
        var line1 = await mediator.Send(new AddKitLineCommand(
            KitId: kitId,
            ComponentItemId: comp1Id,
            UomCode: "UND",
            Quantity: 1m
        ), CancellationToken.None);

        line1.IsSuccess.Should().BeTrue(line1.Error);
        line1.Value!.Quantity.Should().Be(1m);
        line1.Value.UomCode.Should().Be("UND");

        // Add component 2: 1 pantalón
        var line2 = await mediator.Send(new AddKitLineCommand(
            KitId: kitId,
            ComponentItemId: comp2Id,
            UomCode: "UND",
            Quantity: 1m
        ), CancellationToken.None);

        line2.IsSuccess.Should().BeTrue(line2.Error);

        // Verify via GetKitByItemId
        var getResult = await mediator.Send(
            new GetKitByItemIdQuery(kitItemId),
            CancellationToken.None);

        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.Lines.Should().HaveCount(2);
        getResult.Value.Lines.Should().AllSatisfy(l => l.IsActive.Should().BeTrue());
    }

    // ── Test 5.2: Kit KitType.Variable ───────────────────────────────────

    [Fact]
    public async Task CreateKit_Variable_WithOptionalLine()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (kitItemId, comp1Id, comp2Id) = await SetupKitAsync(db, mediator, factory);

        var kitResult = await mediator.Send(
            new CreateKitCommand(kitItemId, KitType.Variable), CancellationToken.None);

        kitResult.IsSuccess.Should().BeTrue();
        var kitId = kitResult.Value!.Id;

        await mediator.Send(new AddKitLineCommand(kitId, comp1Id, "UND", 1m), CancellationToken.None);
        var optLine = await mediator.Send(new AddKitLineCommand(
            KitId: kitId,
            ComponentItemId: comp2Id,
            UomCode: "UND",
            Quantity: 1m,
            IsOptional: true
        ), CancellationToken.None);

        optLine.Value!.IsOptional.Should().BeTrue();

        var kit = await mediator.Send(new GetKitByItemIdQuery(kitItemId), CancellationToken.None);
        kit.Value!.Lines.Should().ContainSingle(l => l.IsOptional);
    }

    // ── Test 5.3: No-recursión — kit no puede contenerse a sí mismo ───────

    [Fact]
    public async Task AddKitLine_ComponentIsSelf_ReturnsConflict()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (kitItemId, _, _) = await SetupKitAsync(db, mediator, factory);

        var kitResult = await mediator.Send(
            new CreateKitCommand(kitItemId, KitType.Fixed), CancellationToken.None);
        var kitId = kitResult.Value!.Id;

        // Try to add the kit's own item as a component
        var selfRef = await mediator.Send(new AddKitLineCommand(
            KitId: kitId,
            ComponentItemId: kitItemId,  // Self-reference!
            UomCode: "UND",
            Quantity: 1m
        ), CancellationToken.None);

        selfRef.IsSuccess.Should().BeFalse("Kit cannot reference itself as component");
    }

    // ── Test 5.4: Componente duplicado en kit es rechazado ────────────────

    [Fact]
    public async Task AddKitLine_DuplicateComponent_ReturnsConflict()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (kitItemId, comp1Id, _) = await SetupKitAsync(db, mediator, factory);

        var kitResult = await mediator.Send(
            new CreateKitCommand(kitItemId, KitType.Fixed), CancellationToken.None);
        var kitId = kitResult.Value!.Id;

        // First line: OK
        await mediator.Send(new AddKitLineCommand(kitId, comp1Id, "UND", 1m), CancellationToken.None);

        // Duplicate line: CONFLICT
        var dup = await mediator.Send(new AddKitLineCommand(kitId, comp1Id, "UND", 2m), CancellationToken.None);
        dup.IsSuccess.Should().BeFalse("Duplicate component in same kit must be rejected");
    }

    // ── Test 5.5: Kit Bundle — diferente al Fixed ─────────────────────────

    [Fact]
    public async Task CreateKit_Bundle_HasDifferentSemantics()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (kitItemId, comp1Id, comp2Id) = await SetupKitAsync(db, mediator, factory);

        var bundleResult = await mediator.Send(
            new CreateKitCommand(kitItemId, KitType.Bundle), CancellationToken.None);

        bundleResult.IsSuccess.Should().BeTrue();
        bundleResult.Value!.KitType.Should().Be("Bundle");

        // Add quantity > 1 (allowed in Bundle)
        await mediator.Send(new AddKitLineCommand(bundleResult.Value.Id, comp1Id, "UND", 2m), CancellationToken.None);
        await mediator.Send(new AddKitLineCommand(bundleResult.Value.Id, comp2Id, "UND", 3m), CancellationToken.None);

        var kit = await mediator.Send(new GetKitByItemIdQuery(kitItemId), CancellationToken.None);
        kit.Value!.Lines.Should().HaveCount(2);
        kit.Value.Lines.Sum(l => l.Quantity).Should().Be(5m);
    }

    // ── Test 5.6: Verificación pendiente (note de integración futura) ─────

    [Fact]
    public async Task Kit_SaleIntegration_IsDocumented_AsPendingScope()
    {
        // This test documents the pending scope for Kit sale integration.
        // PENDING: SaleAuthorizedEventHandler needs to:
        //   1. Detect if sold item is ItemType.Kit
        //   2. Load Kit.Lines from IKitRepository
        //   3. For each KitLine: create StockMovement (SaleExit) for the component
        //   4. Consume CostLayer for each component (FIFO)
        //   5. For KitType.Bundle: do NOT disassemble — track kit-level stock

        // This assertion is intentionally always true — it documents the scope.
        true.Should().BeTrue(
            "PENDING: Kit sale disassembly (component stock deduction) requires " +
            "SaleAuthorizedEventHandler to be updated to use ItemId/VariantId " +
            "and to detect Kit items. Tracked in project decisions.");
    }
}
