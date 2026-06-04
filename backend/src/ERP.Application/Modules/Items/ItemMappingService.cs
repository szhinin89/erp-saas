using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;

namespace ERP.Application.Items;

/// <summary>
/// Mapeo centralizado Item → DTOs.
/// Sin AutoMapper. Sin dependencias externas.
/// </summary>
internal static class ItemMappingService
{
    public static ItemDto ToDto(Item item) => new(
        item.Id,
        item.Code.SKU,
        item.Code.PurchaseCode,
        item.Code.ShortName,
        item.Code.Description,
        item.ItemType.ToString(),
        item.CategoryNodeId,
        item.BrandId,
        item.DefaultUomCode,
        item.SaleConfig.IsForSale,
        item.SaleConfig.IsFavorite,
        item.SaleConfig.IsEcommerceActive,
        item.StockConfig.TracksStock,
        item.StockConfig.TracksLot,
        item.StockConfig.TracksSeries,
        item.IsActive,
        item.CreatedAt,
        item.UpdatedAt);

    public static ItemDetailDto ToDetailDto(Item item) => new(
        item.Id,
        item.Code.SKU,
        item.Code.PurchaseCode,
        item.Code.ShortName,
        item.Code.Description,
        item.Observations,
        item.ItemType.ToString(),
        item.CategoryNodeId,
        item.BrandId,
        item.DefaultUomCode,
        MapTaxConfig(item),
        MapSaleConfig(item),
        MapStockConfig(item),
        item.Variants.Select(MapVariant).ToList(),
        item.Images.Select(MapImage).ToList(),
        item.UnitConversions.Select(MapConversion).ToList(),
        item.Substitutes.Select(MapSubstitute).ToList(),
        item.PackagingLevels.Select(MapPackaging).ToList(),
        item.IsActive,
        item.CreatedAt,
        item.UpdatedAt);

    private static ItemTaxConfigDto MapTaxConfig(Item item) => new(
        item.TaxConfig.AppliesVatOnSale,
        item.TaxConfig.AppliesVatOnPurchase,
        item.TaxConfig.AppliesExciseTax,
        item.TaxConfig.SaleVatCode,
        item.TaxConfig.PurchaseVatCode,
        item.TaxConfig.ExciseTaxCode,
        item.TaxConfig.VatAccountId,
        item.TaxConfig.PurchaseVatAccountId,
        item.TaxConfig.ExciseAccountId,
        item.TaxConfig.SriServiceCode);

    private static ItemSaleConfigDto MapSaleConfig(Item item) => new(
        item.SaleConfig.IsForSale,
        item.SaleConfig.MaxDiscountPercent,
        item.SaleConfig.IsAvailableOnWeb,
        item.SaleConfig.IsAvailableOnPOS,
        item.SaleConfig.IsAvailableOnMobile,
        item.SaleConfig.IsEcommerceActive,
        item.SaleConfig.IsFavorite);

    private static ItemStockConfigDto MapStockConfig(Item item) => new(
        item.StockConfig.TracksStock,
        item.StockConfig.TracksLot,
        item.StockConfig.TracksSeries,
        item.StockConfig.AllowDecimalQty,
        item.StockConfig.AllowDecimalSale,
        item.StockConfig.MinStockQty,
        item.StockConfig.MaxStockQty);

    private static ItemVariantDto MapVariant(ItemVariant v) => new(
        v.Id,
        v.SKU,
        v.Name,
        v.IsDefault,
        v.SortOrder,
        v.IsActive,
        v.Attributes.Select(a => new VariantAttributeDto(a.AttributeDefinitionId, a.Value)).ToList(),
        v.Barcodes.Where(b => b.IsActive).Select(b => new VariantBarcodeDto(b.Id, b.Code, b.BarcodeType.ToString())).ToList());

    private static ItemImageDto MapImage(ItemImage i) => new(
        i.Id, i.VariantId, i.StorageObjectId, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder, i.IsActive);

    private static ItemUnitConversionDto MapConversion(ItemUnitConversion c) => new(
        c.Id, c.FromUomCode, c.ToUomCode, c.Factor, c.IsActive);

    private static ItemSubstituteDto MapSubstitute(ItemSubstitute s) => new(
        s.Id, s.SubstituteItemId, s.Priority, s.Note, s.IsActive);

    private static ItemPackagingLevelDto MapPackaging(ItemPackagingLevel p) => new(
        p.Id, p.Name, p.Level, p.BaseQuantity, p.UomCode,
        p.Barcode, p.Weight, p.IsBaseUnit, p.IsPurchaseDefault, p.IsSaleDefault, p.IsActive);
}
