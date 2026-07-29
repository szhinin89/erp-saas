using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Items.UseCases.GetItems;

public sealed record GetItemsQuery(
    string? Search = null,
    string? Sku = null,
    bool? IsActive = null,
    bool? IsForSale = null,
    bool? IsFavorite = null,
    bool? IsEcommerce = null,
    Guid? ItemTypeId = null,
    Guid? CategoryNodeId = null,
    Guid? BrandId = null,
    string? Barcode = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<GetItemsResponse>>, ICompanyScopedRequest;

public sealed class GetItemsQueryHandler : IRequestHandler<GetItemsQuery, Result<GetItemsResponse>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public GetItemsQueryHandler(
        IItemRepository repository,
        ICurrentTenant tenant,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo
    )
    {
        _repository = repository;
        _currentTenant = tenant;
        _sri = sri;
        _itemTypeRepo = itemTypeRepo;
    }

    public async Task<Result<GetItemsResponse>> Handle(
        GetItemsQuery query,
        CancellationToken cancellationToken
    )
    {
        var filter = new ItemReportFilter(
            query.Search,
            query.Sku,
            query.IsActive,
            query.IsForSale,
            query.IsFavorite,
            query.IsEcommerce,
            query.ItemTypeId,
            query.CategoryNodeId,
            query.BrandId,
            query.Barcode
        );

        var (items, total) = await _repository.GetPageAsync(
            _currentTenant.TenantId,
            filter,
            query.PageNumber,
            query.PageSize,
            cancellationToken
        );

        var uomMap = await _sri.ResolveUomsAsync(
            items.Select(i => i.DefaultUomCode),
            cancellationToken
        );

        var itemTypes = await _itemTypeRepo.ListAsync(
            _currentTenant.TenantId,
            onlyActive: false,
            cancellationToken
        );
        var itemTypeNames = itemTypes.ToDictionary(t => t.Id, t => t.Name);

        var response = new GetItemsResponse(
            items.Select(i => ItemMappingService.ToDto(i, uomMap, itemTypeNames)).ToList(),
            total,
            query.PageNumber,
            query.PageSize
        );

        return Result<GetItemsResponse>.Success(response);
    }
}
