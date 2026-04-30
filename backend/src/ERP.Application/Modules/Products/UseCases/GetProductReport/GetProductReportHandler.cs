using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductReport;

public class GetProductReportHandler
{
    private readonly IProductRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetProductReportHandler(IProductRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<PagedResult<ProductReportItemDto>>> HandleAsync(
        ProductReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var (products, totalCount) = await _repository.GetReportPageAsync(tenantId, filter, pageNumber, pageSize, ct);

        var dtos = products.Select(p => new ProductReportItemDto(
            p.Id,
            p.SaleCode,
            p.PurchaseCode,
            p.ShortName,
            p.Description,
            p.IsFavorite,
            p.IsForSale,
            p.IsActive,
            p.IsEcommerceActive,
            p.IsService,
            p.LineId,
            p.CategoryId,
            p.SubcategoryId,
            p.BrandId,
            p.ProductTypeId,
            p.CreatedAt)).ToList();

        return Result<PagedResult<ProductReportItemDto>>.Success(new PagedResult<ProductReportItemDto>(
            Items: dtos,
            PageNumber: pageNumber,
            PageSize: pageSize,
            TotalCount: totalCount));
    }
}

