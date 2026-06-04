using ERP.Domain.Modules.Items.Enums;

namespace ERP.Application.Items.DTOs;

// ── List / create response ─────────────────────────────────────────────────
public record ItemDto(
    Guid       Id,
    string     SKU,
    string?    PurchaseCode,
    string     ShortName,
    string     Description,
    string     ItemType,
    Guid?      CategoryNodeId,
    Guid?      BrandId,
    string     DefaultUomCode,
    bool       IsForSale,
    bool       IsFavorite,
    bool       IsEcommerceActive,
    bool       TracksStock,
    bool       TracksLot,
    bool       TracksSeries,
    bool       IsActive,
    DateTime   CreatedAt,
    DateTime?  UpdatedAt
);

// ── Nested VOs ─────────────────────────────────────────────────────────────
public record ItemTaxConfigDto(
    bool    AppliesVatOnSale,
    bool    AppliesVatOnPurchase,
    bool    AppliesExciseTax,
    string? SaleVatCode,
    string? PurchaseVatCode,
    string? ExciseTaxCode,
    Guid?   VatAccountId,
    Guid?   PurchaseVatAccountId,
    Guid?   ExciseAccountId,
    string? SriServiceCode
);

public record ItemSaleConfigDto(
    bool     IsForSale,
    decimal? MaxDiscountPercent,
    bool     IsAvailableOnWeb,
    bool     IsAvailableOnPOS,
    bool     IsAvailableOnMobile,
    bool     IsEcommerceActive,
    bool     IsFavorite
);

public record ItemStockConfigDto(
    bool     TracksStock,
    bool     TracksLot,
    bool     TracksSeries,
    bool     AllowDecimalQty,
    bool     AllowDecimalSale,
    decimal? MinStockQty,
    decimal? MaxStockQty
);

// ── Child entity DTOs ──────────────────────────────────────────────────────
public record ItemVariantDto(
    Guid                             Id,
    string                           SKU,
    string                           Name,
    bool                             IsDefault,
    int                              SortOrder,
    bool                             IsActive,
    IReadOnlyList<VariantAttributeDto> Attributes,
    IReadOnlyList<VariantBarcodeDto>   Barcodes
);

public record VariantAttributeDto(Guid AttributeDefinitionId, string Value);
public record VariantBarcodeDto(Guid Id, string Code, string BarcodeType);

public record ItemImageDto(
    Guid    Id,
    Guid?   VariantId,
    Guid    StorageObjectId,
    string? AltText,
    bool    IsMain,
    bool    IsEcommerce,
    int     SortOrder,
    bool    IsActive
);

public record ItemUnitConversionDto(
    Guid    Id,
    string  FromUomCode,
    string  ToUomCode,
    decimal Factor,
    bool    IsActive
);

public record ItemSubstituteDto(
    Guid    Id,
    Guid    SubstituteItemId,
    int     Priority,
    string? Note,
    bool    IsActive
);

public record ItemPackagingLevelDto(
    Guid     Id,
    string   Name,
    int      Level,
    decimal  BaseQuantity,
    string   UomCode,
    string?  Barcode,
    decimal? Weight,
    bool     IsBaseUnit,
    bool     IsPurchaseDefault,
    bool     IsSaleDefault,
    bool     IsActive
);

// ── Detail (full load) ─────────────────────────────────────────────────────
public record ItemDetailDto(
    Guid                               Id,
    string                             SKU,
    string?                            PurchaseCode,
    string                             ShortName,
    string                             Description,
    string?                            Observations,
    string                             ItemType,
    Guid?                              CategoryNodeId,
    Guid?                              BrandId,
    string                             DefaultUomCode,
    ItemTaxConfigDto                   TaxConfig,
    ItemSaleConfigDto                  SaleConfig,
    ItemStockConfigDto                 StockConfig,
    IReadOnlyList<ItemVariantDto>      Variants,
    IReadOnlyList<ItemImageDto>        Images,
    IReadOnlyList<ItemUnitConversionDto> UnitConversions,
    IReadOnlyList<ItemSubstituteDto>   Substitutes,
    IReadOnlyList<ItemPackagingLevelDto> PackagingLevels,
    bool                               IsActive,
    DateTime                           CreatedAt,
    DateTime?                          UpdatedAt
);

// ── Paginated list ─────────────────────────────────────────────────────────
public record GetItemsResponse(
    IReadOnlyList<ItemDto> Items,
    int                    TotalCount,
    int                    PageNumber,
    int                    PageSize
);
