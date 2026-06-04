using MediatR;
using ERP.Application.Common;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.GetStockByItem;

public record StockByItemDto(
    Guid     ItemId,
    Guid?    VariantId,
    Guid     WarehouseId,
    decimal  Quantity,
    decimal  ReservedQuantity,
    decimal  AvailableQuantity,
    decimal  TotalCostValue,
    decimal  AverageCost,
    DateTime LastUpdatedAt
);

public sealed record GetStockByItemQuery(Guid ItemId, Guid? WarehouseId = null)
    : IRequest<Result<IReadOnlyList<StockByItemDto>>>, ICompanyScopedRequest;

public sealed class GetStockByItemQueryHandler
    : IRequestHandler<GetStockByItemQuery, Result<IReadOnlyList<StockByItemDto>>>
{
    private readonly IItemRepository _repository;

    public GetStockByItemQueryHandler(IItemRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<StockByItemDto>>> Handle(
        GetStockByItemQuery query, CancellationToken ct)
    {
        var rows = await _repository.GetStockByItemAsync(query.ItemId, query.WarehouseId, ct);

        var dtos = rows.Select(r => new StockByItemDto(
            r.ItemId, r.VariantId, r.WarehouseId,
            r.Quantity, r.ReservedQuantity, r.AvailableQuantity,
            r.TotalCostValue,
            r.Quantity > 0 ? r.TotalCostValue / r.Quantity : 0m,
            r.LastUpdatedAt)).ToList();

        return Result<IReadOnlyList<StockByItemDto>>.Success(dtos);
    }
}
