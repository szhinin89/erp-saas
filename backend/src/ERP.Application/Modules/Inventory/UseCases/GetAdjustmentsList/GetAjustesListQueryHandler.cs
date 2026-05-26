using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetAdjustmentsList;

public sealed class GetStockAdjustmentsListQueryHandler
    : IRequestHandler<GetStockAdjustmentsListQuery, Result<StockAdjustmentsPagedResult>>
{
    private readonly IStockAdjustmentRepository _repo;
    private readonly ICurrentSubscriber              _currentSubscriber;

    public GetStockAdjustmentsListQueryHandler(
        IStockAdjustmentRepository repo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<StockAdjustmentsPagedResult>> Handle(
        GetStockAdjustmentsListQuery query, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;

        var (items, total) = await _repo.GetPagedAsync(
            subscriberId, query.PageNumber, query.PageSize,
            query.WarehouseId, query.ProductId, query.Status,
            query.DateFrom, query.DateTo, ct);

        var dtos = items.Select(a => new StockAdjustmentDto(
            a.Id, a.AdjustmentNumber,
            a.WarehouseId, a.WarehouseName,
            a.ProductId, a.ProductName,
            a.AdjustmentQty, a.AdjustmentType,
            a.Reason, a.Notes,
            a.AdjustmentDate, a.Status,
            a.ExecutedAt, a.ExecutedBy,
            a.CreatedAt)).ToList();

        return Result<StockAdjustmentsPagedResult>.Success(
            new StockAdjustmentsPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}
