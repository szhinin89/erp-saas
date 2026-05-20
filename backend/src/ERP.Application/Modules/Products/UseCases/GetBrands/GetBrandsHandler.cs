using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetBrands;

public class GetBrandsHandler : IRequestHandler<GetBrandsQuery, Result<IReadOnlyList<BrandDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetBrandsHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<IReadOnlyList<BrandDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetBrandsQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(GetBrandsQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetBrandsAsync(subscriberId, request.OnlyActive, ct);
        var dtos = items.Select(x => new BrandDto(x.Id, x.Code, x.Name, x.IsActive, x.Manufacturer, x.CountryOfOrigin)).ToList();
        return Result<IReadOnlyList<BrandDto>>.Success(dtos);
    }
}

