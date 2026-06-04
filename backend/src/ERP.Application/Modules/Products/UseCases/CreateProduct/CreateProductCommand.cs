using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateProduct;

/// <summary>Creación de producto; requiere feature de inventario/catálogo en el plan SaaS.</summary>
[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreateProductCommand(
    string SaleCode,
    string ShortName,
    string Description,
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
    bool AppliesExciseTax = false,
    string? IceCode = null,
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
    IReadOnlyList<BarcodeInput>? Barcodes = null,
    IReadOnlyList<UnitConversionInput>? UnitConversions = null,
    IReadOnlyList<ImageInput>? Images = null,
    IReadOnlyList<SubstituteInput>? Substitutes = null,
    bool IsForSale = true
) : IRequest<Result<ProductDto>>, ICompanyScopedRequest;

public record BarcodeInput(string Code, int Type);
public record UnitConversionInput(string AlternateUomCode, decimal ConversionFactor);
public record ImageInput(string Url, string? AltText = null, bool IsMain = false, bool IsEcommerce = false, int SortOrder = 0);
public record SubstituteInput(Guid SubstituteProductId, string? Note = null);
