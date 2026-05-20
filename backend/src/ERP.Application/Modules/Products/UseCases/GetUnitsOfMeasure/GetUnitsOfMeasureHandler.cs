using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetUnitsOfMeasure;

public class GetUnitsOfMeasureHandler : IRequestHandler<GetUnitsOfMeasureQuery, Result<IReadOnlyList<UnitOfMeasureDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetUnitsOfMeasureHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<IReadOnlyList<UnitOfMeasureDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetUnitsOfMeasureQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<UnitOfMeasureDto>>> Handle(GetUnitsOfMeasureQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetUnitsOfMeasureAsync(subscriberId, request.OnlyActive, ct);
        var dtos = items.Select(x => new UnitOfMeasureDto(x.Id, x.Code, x.Name, x.Symbol, x.IsActive)).ToList();
        return Result<IReadOnlyList<UnitOfMeasureDto>>.Success(dtos);
    }
}

