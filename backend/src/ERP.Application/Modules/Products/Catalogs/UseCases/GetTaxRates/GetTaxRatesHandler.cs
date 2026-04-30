using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetTaxRates;

public class GetTaxRatesHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetTaxRatesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<TaxRateDto>>> HandleAsync(TaxRateType? type, bool onlyActive, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetTaxRatesAsync(tenantId, type, onlyActive, ct);
        var dtos = items.Select(x => new TaxRateDto(x.Id, x.Code, x.Name, x.Type, x.Percentage, x.IsActive)).ToList();
        return Result<IReadOnlyList<TaxRateDto>>.Success(dtos);
    }
}

