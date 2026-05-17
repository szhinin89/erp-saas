using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.GetProductCategories;

public class GetProductCategoriesHandler : IRequestHandler<GetProductCategoriesQuery, Result<IReadOnlyList<ProductCategoryListItemDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductCategoriesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<ProductCategoryListItemDto>>> Handle(
        GetProductCategoriesQuery query,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductCategoryListRowsAsync(tenantId, query.LineId, query.ActiveFilter, query.Search, ct);
        var dtos = items
            .Select(x => new ProductCategoryListItemDto(x.Id, x.Code, x.Name, x.LineId, x.LineCode, x.LineName, x.IsActive))
            .ToList();
        return Result<IReadOnlyList<ProductCategoryListItemDto>>.Success(dtos);
    }
}

