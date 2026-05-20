using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.GetProductSubcategories;

public class GetProductSubcategoriesHandler : IRequestHandler<GetProductSubcategoriesQuery, Result<IReadOnlyList<ProductSubcategoryListItemDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetProductSubcategoriesHandler(IProductCatalogRepository repo, ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<ProductSubcategoryListItemDto>>> Handle(
        GetProductSubcategoriesQuery query,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var items = await _repo.GetProductSubcategoryListRowsAsync(subscriberId, query.LineId, query.CategoryId, query.ActiveFilter, query.Search, ct);
        var dtos = items
            .Select(x => new ProductSubcategoryListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.CategoryId,
                x.LineId,
                x.LineCode,
                x.LineName,
                x.CategoryCode,
                x.CategoryName,
                x.IsActive))
            .ToList();
        return Result<IReadOnlyList<ProductSubcategoryListItemDto>>.Success(dtos);
    }
}

