using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Costing.Entities;
using ERP.Domain.Modules.Costing.Enums;
using ERP.Infrastructure.Persistence;
using ERP.API.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Integration.Items;

/// <summary>
/// Seed helpers for Items BC integration tests.
/// Extends the base IntegrationSeedData pattern with Items-specific entities.
/// </summary>
internal static class ItemsTestSeedData
{
    // ── Minimal Item seed ─────────────────────────────────────────────────

    internal static Item CreatePhysicalItem(
        Guid subscriberId, Guid userId,
        string sku = "TEST-SKU-001",
        string shortName = "Ítem de prueba",
        bool tracksLot = false,
        bool tracksSeries = false)
    {
        return Item.Create(
            subscriberId,
            sku,
            shortName,
            "Descripción de ítem de prueba para integración",
            ItemType.Physical,
            "UND",
            ItemTaxConfig.Create(
                appliesVatOnSale: false, saleVatCode: null, vatAccountId: null,
                appliesVatOnPurchase: false, purchaseVatCode: null, purchaseVatAccountId: null),
            ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(tracksStock: true, tracksLot: tracksLot, tracksSeries: tracksSeries),
            userId);
    }

    internal static Item CreateServiceItem(Guid subscriberId, Guid userId, string sku = "SVC-001")
    {
        return Item.Create(
            subscriberId, sku, "Servicio de prueba", "Servicio integración",
            ItemType.Service, "HRS",
            ItemTaxConfig.Create(false, null, null, false, null, null),
            ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(tracksStock: false),
            userId);
    }

    internal static Item CreateDigitalItem(Guid subscriberId, Guid userId, string sku = "DIG-001")
    {
        return Item.Create(
            subscriberId, sku, "Software prueba", "Software integración test",
            ItemType.Digital, "LIC",
            ItemTaxConfig.Create(false, null, null, false, null, null),
            ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(tracksStock: false),
            userId);
    }

    internal static Item CreateKitItem(Guid subscriberId, Guid userId, string sku = "KIT-001")
    {
        return Item.Create(
            subscriberId, sku, "Kit de prueba", "Kit integración test",
            ItemType.Kit, "UND",
            ItemTaxConfig.Create(false, null, null, false, null, null),
            ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(tracksStock: false),
            userId);
    }

    // ── Attribute setup ───────────────────────────────────────────────────

    internal static (AttributeGroup group, AttributeDefinition colorDef, AttributeDefinition sizeDef)
        CreateVariantAxes(Guid subscriberId, Guid userId)
    {
        // AttributeGroup.Create already calls SetCreated internally
        var group = AttributeGroup.Create(subscriberId, "VARIANT", "Ejes variante", 0);

        var colorDef = AttributeDefinition.Create(
            subscriberId, group.Id, "COLOR", "Color",
            AttributeDataType.Color, isVariantAxis: true);
        var sizeDef = AttributeDefinition.Create(
            subscriberId, group.Id, "SIZE", "Talla",
            AttributeDataType.List, isVariantAxis: true,
            allowedValues: """[{"value":"S","label":"S"},{"value":"M","label":"M"},{"value":"L","label":"L"}]""");

        return (group, colorDef, sizeDef);
    }

    // ── CostLayer seed ────────────────────────────────────────────────────

    internal static CostLayer CreateCostLayer(
        Guid subscriberId, Guid companyId,
        Guid itemId, Guid warehouseId,
        decimal entryQty, decimal unitCost,
        Guid createdBy,
        DateTime? layerDate = null,
        Guid? variantId = null,
        Guid? lotId = null)
    {
        return CostLayer.Create(
            subscriberId, companyId,
            itemId, warehouseId,
            sourceDocumentId: Guid.NewGuid(),
            sourceDocumentType: "PurchaseReceipt",
            entryQty, unitCost,
            CostMethod.FIFO,
            createdBy,
            variantId, lotId,
            layerDate);
    }
}
