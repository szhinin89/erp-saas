namespace ERP.Application.Items.DTOs;

// ── List / create response ─────────────────────────────────────────────────
public record ItemDto(
    Guid Id,
    string SKU,
    string ShortName,
    string Description,
    Guid ItemTypeId,
    string ItemTypeName,
    Guid? CategoryNodeId,
    Guid? BrandId,
    string DefaultUomCode,
    string DefaultUomAbbrev,
    bool IsForSale,
    bool IsFavorite,
    bool IsEcommerceActive,
    bool TracksStock,
    bool TracksLot,
    bool TracksSeries,
    decimal? BaseSalePrice,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ── Nested VOs ─────────────────────────────────────────────────────────────
public record ItemTaxConfigDto(
    string? SaleVatCode,
    string? SaleVatName,
    string? PurchaseVatCode,
    string? PurchaseVatName,
    string? ExciseTaxCode,
    string? ExciseTaxName
);

public record ItemSaleConfigDto(
    bool IsForSale,
    decimal? MaxDiscountPercent,
    bool IsAvailableOnWeb,
    bool IsAvailableOnPOS,
    bool IsAvailableOnMobile,
    bool IsEcommerceActive,
    bool IsFavorite
);

public record ItemStockConfigDto(
    bool TracksStock,
    bool TracksLot,
    bool TracksSeries,
    bool AllowDecimalQty,
    bool AllowDecimalSale,
    decimal? MinStockQty,
    decimal? MaxStockQty
);

// ── Child entity DTOs ──────────────────────────────────────────────────────
public record ItemVariantDto(
    Guid Id,
    string SKU,
    string Name,
    bool IsDefault,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<VariantAttributeDto> Attributes,
    IReadOnlyList<VariantBarcodeDto> Barcodes
);

public record VariantAttributeDto(Guid AttributeDefinitionId, string Value);
public record VariantBarcodeDto(Guid Id, string Code, string BarcodeType, bool IsPrimary);

public record ItemImageDto(
    Guid Id,
    Guid? VariantId,
    Guid StorageObjectId,
    string? AltText,
    bool IsMain,
    bool IsEcommerce,
    int SortOrder,
    bool IsActive
);

public record ItemUnitConversionDto(
    Guid Id,
    string FromUomCode,
    string FromUomAbbrev,
    string ToUomCode,
    string ToUomAbbrev,
    decimal Factor,
    bool IsActive
);

public record ItemSubstituteDto(
    Guid Id,
    Guid SubstituteItemId,
    int Priority,
    string? Note,
    bool IsActive
);

public record ItemPackagingLevelDto(
    Guid Id,
    string Name,
    int Level,
    decimal BaseQuantity,
    string UomCode,
    string UomAbbrev,
    string? Barcode,
    decimal? Weight,
    bool IsBaseUnit,
    bool IsPurchaseDefault,
    bool IsSaleDefault,
    bool IsActive
);

public record ItemSupplierCodeDto(
    Guid Id,
    Guid SupplierId,
    string Code,
    bool IsPrimary,
    bool IsActive
);

// ── Detail (full load) ─────────────────────────────────────────────────────
public record ItemDetailDto(
    Guid Id,
    string SKU,
    string ShortName,
    string Description,
    string? Observations,
    Guid ItemTypeId,
    string ItemTypeName,
    Guid? CategoryNodeId,
    Guid? BrandId,
    string DefaultUomCode,
    string DefaultUomAbbrev,
    string DefaultUomName,
    ItemTaxConfigDto TaxConfig,
    ItemSaleConfigDto SaleConfig,
    ItemStockConfigDto StockConfig,
    IReadOnlyList<ItemVariantDto> Variants,
    IReadOnlyList<ItemImageDto> Images,
    IReadOnlyList<ItemUnitConversionDto> UnitConversions,
    IReadOnlyList<ItemSubstituteDto> Substitutes,
    IReadOnlyList<ItemPackagingLevelDto> PackagingLevels,
    IReadOnlyList<ItemSupplierCodeDto> SupplierCodes,
    decimal? BaseSalePrice,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ── Paginated list ─────────────────────────────────────────────────────────
public record GetItemsResponse(
    IReadOnlyList<ItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
