namespace ERP.Application.Products.UseCases.CreateProduct;

public record CreateProductCommand(
    string SaleCode,
    string ShortName,
    string Description,
    Guid LineId,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid UnitOfMeasureId,
    Guid BrandId,
    Guid ProductTypeId,
    Guid TariffId,
    bool AppliesVatOnSale,
    Guid? SaleTaxId,
    Guid? SaleVatAccountId,
    bool AppliesVatOnPurchase,
    Guid? PurchaseTaxId,
    Guid? PurchaseVatAccountId,
    bool AppliesExciseTax = false,
    Guid? ExciseTaxId = null,
    Guid? ExciseAccountId = null,
    string? PurchaseCode = null,
    bool IsService = false,
    bool TracksStock = true,
    bool TracksLot = false,
    bool TracksSeries = false,
    bool HasRecipe = false,
    Guid? RecipeId = null,
    bool StockWithDecimal = false,
    bool SaleWithDecimal = false,
    decimal MaxItemDiscountPercent = 0,
    bool AvailableOnWeb = false,
    bool AvailableOnMobile = false,
    bool IsEcommerceActive = false,
    string? BaseColor = null,
    bool HasMultipleColors = false,
    bool HasSizes = false,
    bool HandlesTariff = false,
    IReadOnlyList<BarcodeInput>? Barcodes = null,
    IReadOnlyList<SupplierCodeInput>? SupplierCodes = null,
    IReadOnlyList<UnitConversionInput>? UnitConversions = null,
    IReadOnlyList<ColorInput>? Colors = null,
    IReadOnlyList<SizeInput>? Sizes = null,
    IReadOnlyList<DimensionInput>? Dimensions = null,
    IReadOnlyList<ImageInput>? Images = null,
    IReadOnlyList<FeatureInput>? Features = null,
    IReadOnlyList<TariffDetailInput>? TariffDetails = null,
    IReadOnlyList<SubstituteInput>? Substitutes = null,
    IReadOnlyList<CustomFieldInput>? CustomFields = null,
    bool IsForSale = true
);

public record BarcodeInput(string Code, int Type);
public record SupplierCodeInput(Guid SupplierId, string Code, bool IsDefault = false);
public record UnitConversionInput(Guid AlternateUnitId, decimal ConversionFactor);
public record ColorInput(string Name, string? HexCode = null);
public record SizeInput(string Name, int SortOrder = 0);
public record DimensionInput(string Name, string Value, string Unit);
public record ImageInput(string Url, string? AltText = null, bool IsMain = false, bool IsEcommerce = false, int SortOrder = 0);
public record FeatureInput(string Name, string Value);
public record TariffDetailInput(string OriginCountry, string TariffCode, decimal Percentage);
public record SubstituteInput(Guid SubstituteProductId, string? Note = null);
public record CustomFieldInput(string FieldName, int FieldType, string FieldValue);