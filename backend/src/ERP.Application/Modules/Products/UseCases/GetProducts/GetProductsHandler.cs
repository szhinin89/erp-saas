using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProducts;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, Result<IReadOnlyList<ProductDto>>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductsHandler(IProductRepository repository, ICurrentSubscriber currentSubscriber)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<IReadOnlyList<ProductDto>>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetProductsQuery(), ct);

    public async Task<Result<IReadOnlyList<ProductDto>>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var products = await _repository.GetAllByTenantAsync(subscriberId, ct);

        var dtos = products.Select(p => new ProductDto(
            p.Id, p.SaleCode, p.PurchaseCode, p.ShortName, p.Description,
            p.LineId, p.CategoryId, p.SubcategoryId, p.UnitOfMeasureId,
            p.BrandId, p.ProductTypeId, p.TariffId,
            p.AppliesVatOnSale, p.SaleTaxId,
            p.AppliesVatOnPurchase, p.PurchaseTaxId,
            p.AppliesExciseTax, p.ExciseTaxId,
            p.IsService, p.TracksStock, p.IsActive, p.AvailableOnWeb,
            p.AvailableOnMobile, p.IsEcommerceActive, p.IsForSale, p.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<ProductDto>>.Success(dtos);
    }
}
