using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetTaxRates;

public class GetTaxRatesHandler : IRequestHandler<GetTaxRatesQuery, Result<IReadOnlyList<TaxRateDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetTaxRatesHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<IReadOnlyList<TaxRateDto>>> HandleAsync(TaxRateType? type, bool onlyActive, CancellationToken ct = default)
        => Handle(new GetTaxRatesQuery(type, onlyActive), ct);

    public async Task<Result<IReadOnlyList<TaxRateDto>>> Handle(GetTaxRatesQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetTaxRatesAsync(subscriberId, request.Type, request.OnlyActive, ct);
        var dtos = items.Select(x => new TaxRateDto(x.Id, x.Code, x.Name, x.Type, x.SriVatCode, x.SriIceCode, x.IsActive)).ToList();
        return Result<IReadOnlyList<TaxRateDto>>.Success(dtos);
    }
}

