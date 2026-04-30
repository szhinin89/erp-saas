using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductSubcategories;

public class GetProductSubcategoriesHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductSubcategoriesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<ProductSubcategoryListItemDto>>> HandleAsync(
        Guid? lineId,
        Guid? categoryId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductSubcategoryListRowsAsync(tenantId, lineId, categoryId, activeFilter, search, ct);
        var dtos = items
            .Select(x => new ProductSubcategoryListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.CategoryId,
                x.LineId,
                x.LineCode,
                x.LineName,
                x.CategoryCode,
                x.CategoryName,
                x.IsActive))
            .ToList();
        return Result<IReadOnlyList<ProductSubcategoryListItemDto>>.Success(dtos);
    }
}

