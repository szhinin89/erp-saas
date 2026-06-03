namespace ERP.Application.Products.DTOs;

public record ProductFullReportDto(
    Guid Id,
    string SaleCode,
    string? PurchaseCode,
    string ShortName,
    string Description,
    string? Observations,

    Guid LineId,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid BrandId,
    Guid ProductTypeId,

    string UomCode,
    IReadOnlyList<ProductUnitConversionDto> UnitConversions,

    bool AppliesVatOnPurchase,
    string? PurchaseVatCode,
    Guid? PurchaseVatAccountId,
    bool AppliesVatOnSale,
    string? SaleVatCode,
    Guid? SaleVatAccountId,
    bool AppliesExciseTax,
    string? IceCode,
    Guid? ExciseAccountId,

    bool IsService,
    bool TracksStock,
    bool TracksSeries,
    bool TracksLot,
    bool HasRecipe,
    Guid? RecipeId,
    bool StockWithDecimal,
    bool SaleWithDecimal,
    decimal MaxItemDiscountPercent,

    bool IsFavorite,
    bool IsForSale,
    bool IsActive,
    bool AvailableOnWeb,
    bool AvailableOnMobile,
    bool IsEcommerceActive,

    string? BaseColor,
    bool HasMultipleColors,
    IReadOnlyList<ProductColorDto> Colors,
    bool HasSizes,
    IReadOnlyList<ProductSizeDto> Sizes,
    IReadOnlyList<ProductDimensionDto> Dimensions,

    IReadOnlyList<ProductBarcodeDto> Barcodes,
    IReadOnlyList<ProductSupplierCodeDto> SupplierCodes,

    IReadOnlyList<ProductImageDto> Images,

    IReadOnlyList<ProductFeatureDto> Features,

    bool HandlesTariff,
    Guid TariffId,
    IReadOnlyList<ProductTariffDetailDto> TariffDetails,

    IReadOnlyList<ProductSubstituteDto> Substitutes,
    IReadOnlyList<ProductCustomFieldDto> CustomFields,

    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ProductBarcodeDto(Guid Id, string Code, int Type);
public record ProductSupplierCodeDto(Guid Id, Guid BusinessPartnerId, string Code, bool IsDefault);
public record ProductUnitConversionDto(Guid Id, string AlternateUomCode, decimal ConversionFactor);
public record ProductColorDto(Guid Id, string Name, string? HexCode);
public record ProductSizeDto(Guid Id, string Name, int SortOrder);
public record ProductDimensionDto(Guid Id, string Name, string Value, string Unit);
public record ProductImageDto(Guid Id, string Url, string? AltText, bool IsMain, bool IsEcommerce, int SortOrder);
public record ProductFeatureDto(Guid Id, string Name, string Value);
public record ProductTariffDetailDto(Guid Id, string OriginCountry, string TariffCode, decimal Percentage);
public record ProductSubstituteDto(Guid Id, Guid SubstituteProductId, string? Note);
public record ProductCustomFieldDto(Guid Id, string FieldName, int FieldType, string FieldValue);

