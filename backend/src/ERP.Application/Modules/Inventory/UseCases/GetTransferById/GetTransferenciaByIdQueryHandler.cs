using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetTransferenciaById;

public sealed class GetTransferByIdQueryHandler
    : IRequestHandler<GetTransferByIdQuery, Result<TransferDetailDto?>>
{
    private readonly IStockTransferRepository _repo;
    private readonly ICurrentSubscriber           _currentSubscriber;

    public GetTransferByIdQueryHandler(
        IStockTransferRepository repo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<TransferDetailDto?>> Handle(
        GetTransferByIdQuery query, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(_currentSubscriber.SubscriberId, query.Id, ct);
        if (t is null)
            return Result<TransferDetailDto?>.Success(null);

        var detalles = t.Lines.Select(d => new TransferDetailItemDto(
            d.Id, d.ProductId, d.Description, d.Quantity)).ToList();

        return Result<TransferDetailDto?>.Success(new TransferDetailDto(
            t.Id, t.TransferNumber,
            t.SourceWarehouseId,
            t.SourceWarehouse?.Name ?? t.SourceWarehouseId.ToString(),
            t.TargetWarehouseId,
            t.TargetWarehouse?.Name ?? t.TargetWarehouseId.ToString(),
            t.TransferDate, t.Status,
            t.Reason, t.Notes,
            t.ConfirmedAt, t.ConfirmedBy,
            t.CreatedAt, detalles));
    }
}
