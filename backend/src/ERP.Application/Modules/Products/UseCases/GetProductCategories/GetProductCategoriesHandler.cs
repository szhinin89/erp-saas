using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.GetProductCategories;

public class GetProductCategoriesHandler : IRequestHandler<GetProductCategoriesQuery, Result<IReadOnlyList<ProductCategoryListItemDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductCategoriesHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<ProductCategoryListItemDto>>> Handle(
        GetProductCategoriesQuery query,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetProductCategoryListRowsAsync(subscriberId, query.LineId, query.ActiveFilter, query.Search, ct);
        var dtos = items
            .Select(x => new ProductCategoryListItemDto(x.Id, x.Code, x.Name, x.LineId, x.LineCode, x.LineName, x.IsActive))
            .ToList();
        return Result<IReadOnlyList<ProductCategoryListItemDto>>.Success(dtos);
    }
}

