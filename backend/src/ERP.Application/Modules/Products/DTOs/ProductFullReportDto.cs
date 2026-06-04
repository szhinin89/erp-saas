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

    IReadOnlyList<ProductBarcodeDto> Barcodes,
    IReadOnlyList<ProductImageDto> Images,

    IReadOnlyList<ProductSubstituteDto> Substitutes,
    Guid TariffId,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ProductBarcodeDto(Guid Id, string Code, int Type);
public record ProductUnitConversionDto(Guid Id, string AlternateUomCode, decimal ConversionFactor);
public record ProductImageDto(Guid Id, string Url, string? AltText, bool IsMain, bool IsEcommerce, int SortOrder);
public record ProductSubstituteDto(Guid Id, Guid SubstituteProductId, string? Note);

