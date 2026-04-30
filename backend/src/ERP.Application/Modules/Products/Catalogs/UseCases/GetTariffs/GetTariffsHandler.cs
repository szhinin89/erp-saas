using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetTariffs;

public class GetTariffsHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetTariffsHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<TariffDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetTariffsAsync(tenantId, onlyActive, ct);
        var dtos = items.Select(x => new TariffDto(x.Id, x.Code, x.Description, x.IsActive)).ToList();
        return Result<IReadOnlyList<TariffDto>>.Success(dtos);
    }
}

