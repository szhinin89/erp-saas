using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductById;

public class GetProductByIdHandler
{
    private readonly IProductRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetProductByIdHandler(IProductRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ProductDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var product  = await _repository.GetByIdAsync(id, tenantId, ct);

        if (product is null)
            return Result<ProductDto>.Failure("Producto no encontrado.");

        return Result<ProductDto>.Success(new ProductDto(
            product.Id, product.SaleCode, product.PurchaseCode, product.ShortName,
            product.Description, product.LineId, product.CategoryId, product.SubcategoryId,
            product.UnitOfMeasureId, product.BrandId, product.ProductTypeId, product.TariffId,
            product.SaleTaxId, product.PurchaseTaxId, product.ExciseTaxId,
            product.IsService, product.IsActive, product.AvailableOnWeb,
            product.AvailableOnMobile, product.IsForSale, product.CreatedAt));
    }
}
