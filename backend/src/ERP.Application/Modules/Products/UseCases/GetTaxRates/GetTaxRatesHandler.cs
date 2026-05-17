using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetTaxRates;

public class GetTaxRatesHandler : IRequestHandler<GetTaxRatesQuery, Result<IReadOnlyList<TaxRateDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetTaxRatesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public Task<Result<IReadOnlyList<TaxRateDto>>> HandleAsync(TaxRateType? type, bool onlyActive, CancellationToken ct = default)
        => Handle(new GetTaxRatesQuery(type, onlyActive), ct);

    public async Task<Result<IReadOnlyList<TaxRateDto>>> Handle(GetTaxRatesQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetTaxRatesAsync(tenantId, request.Type, request.OnlyActive, ct);
        var dtos = items.Select(x => new TaxRateDto(x.Id, x.Code, x.Name, x.Type, x.Percentage, x.IsActive)).ToList();
        return Result<IReadOnlyList<TaxRateDto>>.Success(dtos);
    }
}

