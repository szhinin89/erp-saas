using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateProduct;

public class CreateProductHandler
{
    private readonly IProductRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public CreateProductHandler(
        IProductRepository repository,
        ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ProductDto>> HandleAsync(
        CreateProductCommand command,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;

        var product = Product.Create(
            tenantId,
            command.SaleCode,
            command.ShortName,
            command.Description,
            command.LineId,
            command.CategoryId,
            command.SubcategoryId,
            command.UnitOfMeasureId,
            command.BrandId,
            command.ProductTypeId,
            command.TariffId,
            command.SaleTaxId,
            command.PurchaseTaxId,
            tenantId,
            command.PurchaseCode,
            command.ExciseTaxId,
            command.IsService,
            command.TracksLot,
            command.TracksSeries,
            command.HasRecipe,
            command.StockWithDecimal,
            command.AvailableOnWeb,
            command.AvailableOnMobile,
            command.IsForSale);

        await _repository.AddAsync(product, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(new ProductDto(
            product.Id,
            product.SaleCode,
            product.PurchaseCode,
            product.ShortName,
            product.Description,
            product.LineId,
            product.CategoryId,
            product.SubcategoryId,
            product.UnitOfMeasureId,
            product.BrandId,
            product.ProductTypeId,
            product.SaleTaxId,
            product.PurchaseTaxId,
            product.ExciseTaxId,
            product.IsService,
            product.IsActive,
            product.AvailableOnWeb,
            product.AvailableOnMobile,
            product.IsForSale,
            product.CreatedAt));
    }
}