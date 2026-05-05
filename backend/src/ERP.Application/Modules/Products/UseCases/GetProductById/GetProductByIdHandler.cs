using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductById;

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetProductByIdHandler(IProductRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public Task<Result<ProductDto>> HandleAsync(Guid id, CancellationToken ct = default)
        => Handle(new GetProductByIdQuery(id), ct);

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var product  = await _repository.GetByIdAsync(request.Id, tenantId, ct);

        if (product is null)
            return Result<ProductDto>.Failure("Producto no encontrado.");

        return Result<ProductDto>.Success(new ProductDto(
            product.Id, product.SaleCode, product.PurchaseCode, product.ShortName,
            product.Description, product.LineId, product.CategoryId, product.SubcategoryId,
            product.UnitOfMeasureId, product.BrandId, product.ProductTypeId, product.TariffId,
            product.AppliesVatOnSale, product.SaleTaxId,
            product.AppliesVatOnPurchase, product.PurchaseTaxId,
            product.AppliesExciseTax, product.ExciseTaxId,
            product.IsService, product.TracksStock, product.IsActive, product.AvailableOnWeb,
            product.AvailableOnMobile, product.IsEcommerceActive, product.IsForSale, product.CreatedAt));
    }
}
