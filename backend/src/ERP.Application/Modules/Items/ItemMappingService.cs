using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items;

/// <summary>
/// Mapeo centralizado Item → DTOs.
/// Los diccionarios de catálogos SRI se resuelven en el handler (una query batch por página)
/// y se pasan aquí para evitar N+1. Sin AutoMapper. Sin dependencias externas.
/// El nombre del tipo de ítem se resuelve igual: el handler pasa un diccionario
/// Guid ItemTypeId -> Name (batch de una sola consulta a IItemTypeRepository).
/// </summary>
internal static class ItemMappingService
{
    public static ItemDto ToDto(
        Item item,
        IReadOnlyDictionary<string, SriUomInfo> uomMap,
        IReadOnlyDictionary<Guid, string> itemTypeNames
    ) =>
        new(
            item.Id,
            item.Code.SKU,
            item.Code.ShortName,
            item.Code.Description,
            item.ItemTypeId,
            itemTypeNames.TryGetValue(item.ItemTypeId, out var itemTypeName)
                ? itemTypeName
                : string.Empty,
            item.CategoryNodeId,
            item.BrandId,
            item.DefaultUomCode,
            uomMap.TryGetValue(item.DefaultUomCode, out var uom) ? uom.Abbrev : item.DefaultUomCode,
            item.SaleConfig.IsForSale,
            item.SaleConfig.IsFavorite,
            item.SaleConfig.IsEcommerceActive,
            item.StockConfig.TracksStock,
            item.StockConfig.TracksLot,
            item.StockConfig.TracksSeries,
            item.BaseSalePrice,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt
        );

    /// <summary>
    /// Resuelve catálogos SRI + el nombre del tipo de ítem y mapea a DetailDto en un solo paso.
    /// Usar desde handlers que ya tienen el Item cargado con colecciones.
    /// </summary>
    public static async Task<ItemDetailDto> ToDetailDtoAsync(
        Item item,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo,
        Guid tenantId,
        CancellationToken ct
    )
    {
        var uomCodes = item
            .UnitConversions.SelectMany(c => new[] { c.FromUomCode, c.ToUomCode })
            .Append(item.DefaultUomCode)
            .Concat(item.PackagingLevels.Select(p => p.UomCode));

        var vatCodes = new[] { item.TaxConfig.SaleVatCode, item.TaxConfig.PurchaseVatCode }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Cast<string>();

        var iceCodes = !string.IsNullOrWhiteSpace(item.TaxConfig.ExciseTaxCode)
            ? new[] { item.TaxConfig.ExciseTaxCode }
            : [];

        var uomMap = await sri.ResolveUomsAsync(uomCodes, ct);
        var vatMap = await sri.ResolveVatRatesAsync(vatCodes, ct);
        var iceMap = await sri.ResolveIceRatesAsync(iceCodes, ct);

        var itemType = await itemTypeRepo.GetByIdAsync(tenantId, item.ItemTypeId, ct);
        var itemTypeNames = itemType is null
            ? new Dictionary<Guid, string>()
            : new Dictionary<Guid, string> { [itemType.Id] = itemType.Name };

        return ToDetailDto(item, uomMap, vatMap, iceMap, itemTypeNames);
    }

    public static ItemDetailDto ToDetailDto(
        Item item,
        IReadOnlyDictionary<string, SriUomInfo> uomMap,
        IReadOnlyDictionary<string, SriVatInfo> vatMap,
        IReadOnlyDictionary<string, SriIceInfo> iceMap,
        IReadOnlyDictionary<Guid, string> itemTypeNames
    ) =>
        new(
            item.Id,
            item.Code.SKU,
            item.Code.ShortName,
            item.Code.Description,
            item.Observations,
            item.ItemTypeId,
            itemTypeNames.TryGetValue(item.ItemTypeId, out var itemTypeName)
                ? itemTypeName
                : string.Empty,
            item.CategoryNodeId,
            item.BrandId,
            item.DefaultUomCode,
            uomMap.TryGetValue(item.DefaultUomCode, out var uom) ? uom.Abbrev : item.DefaultUomCode,
            uomMap.TryGetValue(item.DefaultUomCode, out var uomN) ? uomN.Name : item.DefaultUomCode,
            MapTaxConfig(item, vatMap, iceMap),
            MapSaleConfig(item),
            MapStockConfig(item),
            item.Variants.Select(MapVariant).ToList(),
            item.Images.Select(MapImage).ToList(),
            item.UnitConversions.Select(c => MapConversion(c, uomMap)).ToList(),
            item.Substitutes.Select(MapSubstitute).ToList(),
            item.PackagingLevels.Select(p => MapPackaging(p, uomMap)).ToList(),
            item.SupplierCodes.Select(MapSupplierCode).ToList(),
            item.BaseSalePrice,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt
        );

    private static ItemTaxConfigDto MapTaxConfig(
        Item item,
        IReadOnlyDictionary<string, SriVatInfo> vatMap,
        IReadOnlyDictionary<string, SriIceInfo> iceMap
    ) =>
        new(
            item.TaxConfig.SaleVatCode,
            !string.IsNullOrWhiteSpace(item.TaxConfig.SaleVatCode)
            && vatMap.TryGetValue(item.TaxConfig.SaleVatCode, out var sv)
                ? sv.Name
                : null,
            item.TaxConfig.PurchaseVatCode,
            !string.IsNullOrWhiteSpace(item.TaxConfig.PurchaseVatCode)
            && vatMap.TryGetValue(item.TaxConfig.PurchaseVatCode, out var pv)
                ? pv.Name
                : null,
            item.TaxConfig.ExciseTaxCode,
            !string.IsNullOrWhiteSpace(item.TaxConfig.ExciseTaxCode)
            && iceMap.TryGetValue(item.TaxConfig.ExciseTaxCode, out var ice)
                ? ice.Name
                : null
        );

    private static ItemSaleConfigDto MapSaleConfig(Item item) =>
        new(
            item.SaleConfig.IsForSale,
            item.SaleConfig.MaxDiscountPercent,
            item.SaleConfig.IsAvailableOnWeb,
            item.SaleConfig.IsAvailableOnPOS,
            item.SaleConfig.IsAvailableOnMobile,
            item.SaleConfig.IsEcommerceActive,
            item.SaleConfig.IsFavorite
        );

    private static ItemStockConfigDto MapStockConfig(Item item) =>
        new(
            item.StockConfig.TracksStock,
            item.StockConfig.TracksLot,
            item.StockConfig.TracksSeries,
            item.StockConfig.AllowDecimalQty,
            item.StockConfig.AllowDecimalSale,
            item.StockConfig.MinStockQty,
            item.StockConfig.MaxStockQty
        );

    private static ItemVariantDto MapVariant(ItemVariant v) =>
        new(
            v.Id,
            v.SKU,
            v.Name,
            v.IsDefault,
            v.SortOrder,
            v.IsActive,
            v.Attributes.Select(a => new VariantAttributeDto(a.AttributeDefinitionId, a.Value))
                .ToList(),
            v.Barcodes.Where(b => b.IsActive)
                .Select(b => new VariantBarcodeDto(b.Id, b.Code, b.BarcodeType, b.IsPrimary))
                .ToList()
        );

    private static ItemImageDto MapImage(ItemImage i) =>
        new(
            i.Id,
            i.VariantId,
            i.StorageObjectId,
            i.AltText,
            i.IsMain,
            i.IsEcommerce,
            i.SortOrder,
            i.IsActive
        );

    private static ItemUnitConversionDto MapConversion(
        ItemUnitConversion c,
        IReadOnlyDictionary<string, SriUomInfo> uomMap
    ) =>
        new(
            c.Id,
            c.FromUomCode,
            uomMap.TryGetValue(c.FromUomCode, out var from) ? from.Abbrev : c.FromUomCode,
            c.ToUomCode,
            uomMap.TryGetValue(c.ToUomCode, out var to) ? to.Abbrev : c.ToUomCode,
            c.Factor,
            c.IsActive
        );

    private static ItemSubstituteDto MapSubstitute(ItemSubstitute s) =>
        new(s.Id, s.SubstituteItemId, s.Priority, s.Note, s.IsActive);

    private static ItemPackagingLevelDto MapPackaging(
        ItemPackagingLevel p,
        IReadOnlyDictionary<string, SriUomInfo> uomMap
    ) =>
        new(
            p.Id,
            p.Name,
            p.Level,
            p.BaseQuantity,
            p.UomCode,
            uomMap.TryGetValue(p.UomCode, out var uom) ? uom.Abbrev : p.UomCode,
            p.Barcode,
            p.Weight,
            p.IsBaseUnit,
            p.IsPurchaseDefault,
            p.IsSaleDefault,
            p.IsActive
        );

    private static ItemSupplierCodeDto MapSupplierCode(ItemSupplierCode s) =>
        new(s.Id, s.SupplierId, s.PackagingLevelId, s.Code, s.IsPrimary, s.IsActive);
}
