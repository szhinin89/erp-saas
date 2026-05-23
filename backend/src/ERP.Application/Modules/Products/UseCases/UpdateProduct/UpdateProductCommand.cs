using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.CreateProduct;

namespace ERP.Application.Products.UseCases.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    string ShortName,
    string Description,
    string? Observations,
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
    bool AppliesExciseTax,
    Guid? ExciseTaxId,
    Guid? ExciseAccountId,

    bool TracksStock,
    bool IsService,
    bool TracksLot,
    bool TracksSeries,
    bool HasRecipe,
    Guid? RecipeId,
    bool StockWithDecimal,
    bool SaleWithDecimal,
    decimal MaxItemDiscountPercent,

    bool AvailableOnWeb,
    bool AvailableOnMobile,
    bool IsEcommerceActive,
    bool IsForSale,

    string? BaseColor,
    bool HasMultipleColors,
    bool HasSizes,
    bool HandlesTariff,

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
    IReadOnlyList<CustomFieldInput>? CustomFields = null
) : IRequest<Result<ProductDto>>, ICompanyScopedRequest;

