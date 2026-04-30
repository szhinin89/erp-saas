using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductTypes;

public class GetProductTypesHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductTypesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<ProductTypeDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductTypesAsync(tenantId, onlyActive, ct);
        var dtos = items.Select(x => new ProductTypeDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<ProductTypeDto>>.Success(dtos);
    }
}

