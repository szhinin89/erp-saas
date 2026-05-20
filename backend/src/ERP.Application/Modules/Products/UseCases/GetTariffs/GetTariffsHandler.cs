using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetTariffs;

public class GetTariffsHandler : IRequestHandler<GetTariffsQuery, Result<IReadOnlyList<TariffDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetTariffsHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<IReadOnlyList<TariffDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetTariffsQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<TariffDto>>> Handle(GetTariffsQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetTariffsAsync(subscriberId, request.OnlyActive, ct);
        var dtos = items.Select(x => new TariffDto(x.Id, x.Code, x.Description, x.IsActive)).ToList();
        return Result<IReadOnlyList<TariffDto>>.Success(dtos);
    }
}

