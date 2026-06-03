using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductById;

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductByIdHandler(IProductRepository repository, ICurrentSubscriber currentSubscriber)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<ProductDto>> HandleAsync(Guid id, CancellationToken ct = default)
        => Handle(new GetProductByIdQuery(id), ct);

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var product  = await _repository.GetByIdAsync(request.Id, subscriberId, ct);

        if (product is null)
            return Result<ProductDto>.Failure("Producto no encontrado.");

        return Result<ProductDto>.Success(new ProductDto(
            product.Id, product.SaleCode, product.PurchaseCode, product.ShortName,
            product.Description, product.LineId, product.CategoryId, product.SubcategoryId,
            product.UomCode, product.BrandId, product.ProductTypeId, product.TariffId,
            product.AppliesVatOnSale, product.SaleVatCode,
            product.AppliesVatOnPurchase, product.PurchaseVatCode,
            product.AppliesExciseTax, product.IceCode,
            product.IsService, product.TracksStock, product.IsActive, product.AvailableOnWeb,
            product.AvailableOnMobile, product.IsEcommerceActive, product.IsForSale, product.CreatedAt));
    }
}
