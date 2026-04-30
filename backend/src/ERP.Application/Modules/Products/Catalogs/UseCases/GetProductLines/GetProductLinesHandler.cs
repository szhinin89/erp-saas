using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductLines;

public class GetProductLinesHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductLinesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<ProductLineDto>>> HandleAsync(
        bool? activeFilter,
        string? search,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductLinesAsync(tenantId, activeFilter, search, ct);
        var dtos = items.Select(x => new ProductLineDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<ProductLineDto>>.Success(dtos);
    }
}

