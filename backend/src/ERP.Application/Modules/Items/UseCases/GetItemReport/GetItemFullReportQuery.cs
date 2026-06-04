using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.GetItemReport;

/// <summary>
/// Full detail report for a single item.
/// Includes all collections and catalog name lookups.
/// </summary>
public sealed record GetItemFullReportQuery(Guid Id)
    : IRequest<Result<ItemFullReportDto>>, ICompanyScopedRequest;

public sealed class GetItemFullReportQueryHandler
    : IRequestHandler<GetItemFullReportQuery, Result<ItemFullReportDto>>
{
    private readonly IItemRepository _repository;
    private readonly IItemCatalogRepository _catalog;
    private readonly ICurrentSubscriber _subscriber;

    public GetItemFullReportQueryHandler(
        IItemRepository repository,
        IItemCatalogRepository catalog,
        ICurrentSubscriber subscriber)
    { _repository = repository; _catalog = catalog; _subscriber = subscriber; }

    public async Task<Result<ItemFullReportDto>> Handle(GetItemFullReportQuery query, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(query.Id, _subscriber.SubscriberId, ct);
        if (item is null) return Result<ItemFullReportDto>.NotFound("Ítem no encontrado.");

        // Resolve brand name for display
        string? brandName = null;
        if (item.BrandId.HasValue)
        {
            var brand = await _catalog.GetBrandByIdAsync(item.BrandId.Value, ct);
            brandName = brand?.Name;
        }

        var detail = ItemMappingService.ToDetailDto(item);

        var report = new ItemFullReportDto(
            item.Id,
            item.Code.SKU,
            item.Code.PurchaseCode,
            item.Code.ShortName,
            item.Code.Description,
            item.Observations,
            item.ItemType.ToString(),
            item.CategoryNodeId,
            item.BrandId,
            brandName,
            item.DefaultUomCode,
            detail.TaxConfig,
            detail.SaleConfig,
            detail.StockConfig,
            detail.Variants,
            detail.Images,
            detail.UnitConversions,
            detail.Substitutes,
            detail.PackagingLevels,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt);

        return Result<ItemFullReportDto>.Success(report);
    }
}
