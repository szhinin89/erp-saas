using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetTransfersList;

public sealed class GetTransfersListQueryHandler
    : IRequestHandler<GetTransfersListQuery, Result<TransfersPagedResult>>
{
    private readonly IStockTransferRepository _repo;
    private readonly ICurrentSubscriber           _currentSubscriber;

    public GetTransfersListQueryHandler(
        IStockTransferRepository repo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<TransfersPagedResult>> Handle(
        GetTransfersListQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize   = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await _repo.GetPagedAsync(
            _currentSubscriber.SubscriberId,
            pageNumber, pageSize,
            query.SourceWarehouseId, query.TargetWarehouseId,
            query.Status, query.DateFrom, query.DateTo,
            ct);

        var dtos = items.Select(ToDto).ToList();
        return Result<TransfersPagedResult>.Success(
            new TransfersPagedResult(dtos, total, pageNumber, pageSize));
    }

    private static TransferDto ToDto(StockTransfer t) => new(
        t.Id, t.TransferNumber,
        t.SourceWarehouseId,
        t.SourceWarehouse?.Name ?? t.SourceWarehouseId.ToString(),
        t.TargetWarehouseId,
        t.TargetWarehouse?.Name ?? t.TargetWarehouseId.ToString(),
        t.TransferDate, t.Status,
        t.Reason, t.Notes,
        t.ConfirmedAt, t.ConfirmedBy,
        t.CreatedAt);
}
