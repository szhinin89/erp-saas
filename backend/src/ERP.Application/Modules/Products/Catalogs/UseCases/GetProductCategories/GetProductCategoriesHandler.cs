using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductCategories;

public class GetProductCategoriesHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductCategoriesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<ProductCategoryListItemDto>>> HandleAsync(
        Guid? lineId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductCategoryListRowsAsync(tenantId, lineId, activeFilter, search, ct);
        var dtos = items
            .Select(x => new ProductCategoryListItemDto(x.Id, x.Code, x.Name, x.LineId, x.LineCode, x.LineName, x.IsActive))
            .ToList();
        return Result<IReadOnlyList<ProductCategoryListItemDto>>.Success(dtos);
    }
}

