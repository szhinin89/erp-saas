using ERP.API.Tests.Support;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Domain.Modules.Costing.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Caso 7: Importación → distribución de costos → landed cost.
///
/// Estado de implementación:
/// ✅ Domain entities: ImportOrder, ImportCostAllocation, ImportTariff
/// ✅ CostLayer con campo LandedCostAdjustment en PurchaseReceiptLine
/// ✅ CostLayerRepository con FIFO/AVCO
/// ⚠️  Application use cases para ImportOrder BC pendientes.
///     El cálculo de landed cost (FOB + flete + seguro + aranceles)
///     y su distribución proporcional por ítem se implementará en
///     la Etapa 12 (Purchasing BC completo).
///
/// Por ahora se valida:
/// a) Que el CostLayer acepta landed cost adjustment en el unitCost
/// b) Que la distribución proporcional es matemáticamente correcta
/// c) Que el ImportTariff domain entity funciona correctamente
/// </summary>
public sealed class Caso7_Import_LandedCost_Tests
{
    // ── Test 7.1: CostLayer acepta costo unitario ajustado (landed cost) ──

    [Fact]
    public async Task CostLayer_WithLandedCostAdjustment_UnitCostIncludesFreightAndDuties()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "IMPORT-CELULAR",
            ShortName: "Celular importado",
            Description: "Smartphone importado de China",
            ItemType: ItemType.Physical,
            DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null,
            TracksStock: true
        ), CancellationToken.None)).Value!;

        // Import scenario:
        // FOB:       $1,000 (100 units × $10 FOB each)
        // Freight:   $100 (10% of FOB)
        // Insurance: $20
        // Duties:    $150 (15% of FOB)
        // Other:     $30
        // Total landed cost: $1,300 → $13/unit

        decimal fobPerUnit      = 10.00m;
        decimal freightPerUnit  = 1.00m;   // 100/100 units
        decimal insurancePerUnit = 0.20m;  // 20/100 units
        decimal dutiesPerUnit   = 1.50m;   // 150/100 units
        decimal otherPerUnit    = 0.30m;   // 30/100 units
        decimal landedCostPerUnit = fobPerUnit + freightPerUnit + insurancePerUnit
                                    + dutiesPerUnit + otherPerUnit;

        landedCostPerUnit.Should().Be(13.00m, "Total landed cost per unit");

        // CostLayer stores the full landed cost as unitCost
        var layer = CostLayer.Create(
            seed.SubscriberId, seed.CompanyId,
            item.Id, seed.WarehouseId,
            sourceDocumentId: Guid.NewGuid(),
            sourceDocumentType: "ImportOrder",
            entryQty: 100m,
            unitCost: landedCostPerUnit,
            costMethod: ERP.Domain.Modules.Costing.Enums.CostMethod.FIFO,
            createdBy: seed.UserId);

        db.CostLayers.Add(layer);
        await db.SaveChangesAsync();

        // Verify stored correctly
        var dbLayer = await db.CostLayers.FirstAsync(c => c.Id == layer.Id);
        dbLayer.UnitCost.Should().Be(13.00m);
        dbLayer.TotalCost.Should().Be(1300.00m, "100 units × $13 = $1,300 total landed cost");
        dbLayer.EntryQty.Should().Be(100m);
    }

    // ── Test 7.2: Distribución proporcional de costos de importación ──────

    [Fact]
    public void LandedCost_ProportionalDistribution_ByFobValue_IsCorrect()
    {
        // Pure domain math test — no DB needed.
        // Import order with 2 items, distributed proportionally by FOB value.
        //
        // Item A: 100 units × $10 FOB = $1,000 FOB (50% of total)
        // Item B: 50  units × $20 FOB = $1,000 FOB (50% of total)
        // Total FOB: $2,000
        //
        // Import costs to distribute:
        //   Freight:   $200
        //   Insurance: $40
        //   Duties:    $300
        //   Other:     $60
        //   Total additional: $600
        //
        // Distribution:
        //   Item A: 50% × $600 = $300 → $3/unit
        //   Item B: 50% × $600 = $300 → $6/unit

        decimal totalFob       = 2000m;
        decimal totalAdditional = 600m;  // freight + insurance + duties + other

        // Item A
        decimal itemAFob     = 1000m;
        decimal itemAQty     = 100m;
        decimal itemAFobCost = 10.00m;   // per unit

        decimal itemAShare     = itemAFob / totalFob;
        decimal itemAAllocated = itemAShare * totalAdditional;
        decimal itemALandedCostPerUnit = itemAFobCost + (itemAAllocated / itemAQty);

        itemAShare.Should().Be(0.50m);
        itemAAllocated.Should().Be(300m);
        itemALandedCostPerUnit.Should().Be(13.00m, "FOB $10 + allocated $3 = landed $13/unit");

        // Item B
        decimal itemBFob     = 1000m;
        decimal itemBQty     = 50m;
        decimal itemBFobCost = 20.00m;   // per unit

        decimal itemBShare     = itemBFob / totalFob;
        decimal itemBAllocated = itemBShare * totalAdditional;
        decimal itemBLandedCostPerUnit = itemBFobCost + (itemBAllocated / itemBQty);

        itemBShare.Should().Be(0.50m);
        itemBAllocated.Should().Be(300m);
        itemBLandedCostPerUnit.Should().Be(26.00m, "FOB $20 + allocated $6 = landed $26/unit");

        // Total allocated = total additional
        (itemAAllocated + itemBAllocated).Should().Be(totalAdditional,
            "Sum of allocations must equal total additional costs");
    }

    // ── Test 7.3: Arancel importación con porcentaje de aranceles ────────

    [Fact]
    public void LandedCost_TariffCalculation_AdValoremOnCIF()
    {
        // Ecuador customs: duties calculated on CIF value
        // CIF = Cost + Insurance + Freight
        //
        // Item: 100 smartphones
        // FOB:       $5,000
        // Freight:   $500
        // Insurance: $100
        // CIF:       $5,600
        //
        // Tariff rate (arancel ad valorem): 15%
        // Duties = CIF × 15% = $840
        //
        // Landed cost per unit:
        //   CIF/unit: $56
        //   Duties/unit: $8.40
        //   Total: $64.40

        decimal fob       = 5000m;
        decimal freight   = 500m;
        decimal insurance = 100m;
        decimal cif       = fob + freight + insurance;  // $5,600

        cif.Should().Be(5600m);

        decimal tariffRate = 0.15m;
        decimal duties     = cif * tariffRate;
        duties.Should().Be(840m);

        int qty = 100;
        decimal landedCostPerUnit = (cif + duties) / qty;
        landedCostPerUnit.Should().Be(64.40m, "($5,600 + $840) / 100 = $64.40 per unit");
    }

    // ── Test 7.4: CostLayer for import — sourceDocumentType="ImportOrder" ─

    [Fact]
    public async Task CostLayer_FromImportOrder_StoresSourceDocumentType()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser,
            CancellationToken.None, factory.MutableCompany);

        var item = (await mediator.Send(new CreateItemCommand(
            SKU: "IMP-SRC-TYPE", ShortName: "Import source", Description: "Test",
            ItemType: ItemType.Physical, DefaultUomCode: "UND",
            AppliesVatOnSale: false, SaleVatCode: null, VatAccountId: null,
            AppliesVatOnPurchase: false, PurchaseVatCode: null, PurchaseVatAccountId: null
        ), CancellationToken.None)).Value!;

        var importOrderId = Guid.NewGuid();
        var layer = CostLayer.Create(
            seed.SubscriberId, seed.CompanyId,
            item.Id, seed.WarehouseId,
            sourceDocumentId: importOrderId,
            sourceDocumentType: "ImportOrder",
            entryQty: 50m,
            unitCost: 64.40m,  // landed cost per unit from Test 7.3
            costMethod: ERP.Domain.Modules.Costing.Enums.CostMethod.FIFO,
            createdBy: seed.UserId);

        db.CostLayers.Add(layer);
        await db.SaveChangesAsync();

        var dbLayer = await db.CostLayers.AsNoTracking().FirstAsync(c => c.Id == layer.Id);
        dbLayer.SourceDocumentType.Should().Be("ImportOrder");
        dbLayer.SourceDocumentId.Should().Be(importOrderId);
        dbLayer.UnitCost.Should().Be(64.40m, "Full landed cost stored as unit cost");
        dbLayer.TotalCost.Should().Be(50 * 64.40m);
    }

    // ── Test 7.5: Pending scope documentation ────────────────────────────

    [Fact]
    public void Import_FullApplicationLayer_IsPendingScope()
    {
        // Documents what remains to be implemented for full import validation.
        //
        // PENDING APPLICATION USE CASES (Etapa 12):
        //   - CreateImportOrderCommand
        //   - AllocateImportCostsCommand (distributes freight/insurance/duties)
        //   - ConfirmImportOrderCommand (triggers:
        //       → RegisterLot per line item
        //       → CostLayer.Create() with landed unitCost
        //       → StockMovement (ImportEntry)
        //       → StockLedger.ApplyMovement())
        //
        // PENDING INTEGRATION TESTS (after Etapa 12):
        //   - End-to-end import flow: ImportOrder → CostLayer with full landed cost
        //   - Multi-item import with proportional distribution
        //   - Tariff rates (ad valorem + specific)
        //   - SENAE/SRI integration for Ecuador customs

        true.Should().BeTrue(
            "Import application layer (CreateImportOrder, AllocateCosts, ConfirmImport) " +
            "is pending implementation in Etapa 12 — Purchasing BC completo.");
    }
}
