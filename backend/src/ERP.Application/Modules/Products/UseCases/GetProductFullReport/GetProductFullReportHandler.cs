using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductFullReport;

public class GetProductFullReportHandler : IRequestHandler<GetProductFullReportQuery, Result<ProductFullReportDto>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductFullReportHandler(IProductRepository repository, ICurrentSubscriber currentSubscriber)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<ProductFullReportDto>> HandleAsync(Guid id, CancellationToken ct = default)
        => Handle(new GetProductFullReportQuery(id), ct);

    public async Task<Result<ProductFullReportDto>> Handle(GetProductFullReportQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var product  = await _repository.GetByIdAsync(request.Id, subscriberId, ct);

        if (product is null)
            return Result<ProductFullReportDto>.Failure("Producto no encontrado.");

        var dto = new ProductFullReportDto(
            product.Id,
            product.SaleCode,
            product.PurchaseCode,
            product.ShortName,
            product.Description,
            product.Observations,
            product.LineId,
            product.CategoryId,
            product.SubcategoryId,
            product.BrandId,
            product.ProductTypeId,
            product.UnitOfMeasureId,
            product.UnitConversions.Where(u => u.IsActive).Select(u => new ProductUnitConversionDto(u.Id, u.AlternateUnitId, u.ConversionFactor)).ToList(),
            product.AppliesVatOnPurchase,
            product.PurchaseTaxId,
            product.PurchaseVatAccountId,
            product.AppliesVatOnSale,
            product.SaleTaxId,
            product.SaleVatAccountId,
            product.AppliesExciseTax,
            product.ExciseTaxId,
            product.ExciseAccountId,
            product.IsService,
            product.TracksStock,
            product.TracksSeries,
            product.TracksLot,
            product.HasRecipe,
            product.RecipeId,
            product.StockWithDecimal,
            product.SaleWithDecimal,
            product.MaxItemDiscountPercent,
            product.IsFavorite,
            product.IsForSale,
            product.IsActive,
            product.AvailableOnWeb,
            product.AvailableOnMobile,
            product.IsEcommerceActive,
            product.BaseColor,
            product.HasMultipleColors,
            product.Colors.Where(c => c.IsActive).Select(c => new ProductColorDto(c.Id, c.Name, c.HexCode)).ToList(),
            product.HasSizes,
            product.Sizes.Where(s => s.IsActive).OrderBy(s => s.SortOrder).Select(s => new ProductSizeDto(s.Id, s.Name, s.SortOrder)).ToList(),
            product.Dimensions.Where(d => d.IsActive).Select(d => new ProductDimensionDto(d.Id, d.Name, d.Value, d.Unit)).ToList(),
            product.Barcodes.Where(b => b.IsActive).Select(b => new ProductBarcodeDto(b.Id, b.Code, (int)b.Type)).ToList(),
            product.SupplierCodes.Where(s => s.IsActive).Select(s => new ProductSupplierCodeDto(s.Id, s.BusinessPartnerId, s.Code, s.IsDefault)).ToList(),
            product.Images.Where(i => i.IsActive).OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder)).ToList(),
            product.Features.Where(f => f.IsActive).Select(f => new ProductFeatureDto(f.Id, f.Name, f.Value)).ToList(),
            product.HandlesTariff,
            product.TariffId,
            product.TariffDetails.Where(t => t.IsActive).Select(t => new ProductTariffDetailDto(t.Id, t.OriginCountry, t.TariffCode, t.Percentage)).ToList(),
            product.Substitutes.Where(s => s.IsActive).Select(s => new ProductSubstituteDto(s.Id, s.SubstituteProductId, s.Note)).ToList(),
            product.CustomFields.Where(c => c.IsActive).Select(c => new ProductCustomFieldDto(c.Id, c.FieldName, (int)c.FieldType, c.FieldValue)).ToList(),
            product.CreatedAt,
            product.UpdatedAt);

        return Result<ProductFullReportDto>.Success(dto);
    }
}

