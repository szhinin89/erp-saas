using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetBrands;

public class GetBrandsHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetBrandsHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetBrandsAsync(tenantId, onlyActive, ct);
        var dtos = items.Select(x => new BrandDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<BrandDto>>.Success(dtos);
    }
}

