using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.GetProductLines;

public class GetProductLinesHandler : IRequestHandler<GetProductLinesQuery, Result<IReadOnlyList<ProductLineDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductLinesHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<ProductLineDto>>> Handle(
        GetProductLinesQuery query,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetProductLinesAsync(subscriberId, query.ActiveFilter, query.Search, ct);
        var dtos = items.Select(x => new ProductLineDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<ProductLineDto>>.Success(dtos);
    }
}

