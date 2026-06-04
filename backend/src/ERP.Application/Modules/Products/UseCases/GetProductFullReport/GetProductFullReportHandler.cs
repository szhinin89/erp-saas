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
        _repository        = repository;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<ProductFullReportDto>> Handle(GetProductFullReportQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var product = await _repository.GetByIdAsync(request.Id, subscriberId, ct);

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
            product.UomCode,
            product.UnitConversions.Where(u => u.IsActive)
                .Select(u => new ProductUnitConversionDto(u.Id, u.AlternateUomCode, u.ConversionFactor)).ToList(),
            product.AppliesVatOnPurchase,
            product.PurchaseVatCode,
            product.PurchaseVatAccountId,
            product.AppliesVatOnSale,
            product.SaleVatCode,
            product.SaleVatAccountId,
            product.AppliesExciseTax,
            product.IceCode,
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
            product.Barcodes.Where(b => b.IsActive)
                .Select(b => new ProductBarcodeDto(b.Id, b.Code, (int)b.Type)).ToList(),
            product.Images.Where(i => i.IsActive).OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(i.Id, i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder)).ToList(),
            product.Substitutes.Where(s => s.IsActive)
                .Select(s => new ProductSubstituteDto(s.Id, s.SubstituteProductId, s.Note)).ToList(),
            product.TariffId,
            product.CreatedAt,
            product.UpdatedAt);

        return Result<ProductFullReportDto>.Success(dto);
    }
}
