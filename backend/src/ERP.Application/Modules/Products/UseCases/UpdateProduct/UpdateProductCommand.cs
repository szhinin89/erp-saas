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
    string UomCode,
    Guid BrandId,
    Guid ProductTypeId,
    Guid TariffId,

    bool AppliesVatOnSale,
    string? SaleVatCode,
    Guid? SaleVatAccountId,
    bool AppliesVatOnPurchase,
    string? PurchaseVatCode,
    Guid? PurchaseVatAccountId,
    bool AppliesExciseTax,
    string? IceCode,
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

    IReadOnlyList<BarcodeInput>? Barcodes = null,
    IReadOnlyList<UnitConversionInput>? UnitConversions = null,
    IReadOnlyList<ImageInput>? Images = null,
    IReadOnlyList<SubstituteInput>? Substitutes = null
) : IRequest<Result<ProductDto>>, ICompanyScopedRequest;
