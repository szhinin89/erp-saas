using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetUnitsOfMeasure;

public class GetUnitsOfMeasureHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetUnitsOfMeasureHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<UnitOfMeasureDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetUnitsOfMeasureAsync(tenantId, onlyActive, ct);
        var dtos = items.Select(x => new UnitOfMeasureDto(x.Id, x.Code, x.Name, x.Symbol, x.IsActive)).ToList();
        return Result<IReadOnlyList<UnitOfMeasureDto>>.Success(dtos);
    }
}

