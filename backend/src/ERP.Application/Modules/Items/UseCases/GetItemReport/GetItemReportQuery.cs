using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.GetItemReport;

/// <summary>
/// Paginated report with extended filters for administration/export.
/// Returns ItemDto (lightweight) with total count for pagination.
/// </summary>
public sealed record GetItemReportQuery(
    string?   Search         = null,
    string?   Sku            = null,
    bool?     IsActive       = null,
    bool?     IsForSale      = null,
    bool?     IsFavorite     = null,
    bool?     IsEcommerce    = null,
    ItemType? ItemType       = null,
    Guid?     CategoryNodeId = null,
    Guid?     BrandId        = null,
    string?   Barcode        = null,
    int       PageNumber     = 1,
    int       PageSize       = 50
) : IRequest<Result<GetItemsResponse>>, ICompanyScopedRequest;

public sealed class GetItemReportQueryHandler
    : IRequestHandler<GetItemReportQuery, Result<GetItemsResponse>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;

    public GetItemReportQueryHandler(IItemRepository repository, ICurrentSubscriber subscriber)
    { _repository = repository; _subscriber = subscriber; }

    public async Task<Result<GetItemsResponse>> Handle(GetItemReportQuery query, CancellationToken ct)
    {
        var filter = new ItemReportFilter(
            query.Search, query.Sku, query.IsActive, query.IsForSale,
            query.IsFavorite, query.IsEcommerce, query.ItemType,
            query.CategoryNodeId, query.BrandId, query.Barcode);

        var (items, total) = await _repository.GetPageAsync(
            _subscriber.SubscriberId, filter,
            query.PageNumber, query.PageSize, ct);

        return Result<GetItemsResponse>.Success(new GetItemsResponse(
            items.Select(ItemMappingService.ToDto).ToList(),
            total, query.PageNumber, query.PageSize));
    }
}
