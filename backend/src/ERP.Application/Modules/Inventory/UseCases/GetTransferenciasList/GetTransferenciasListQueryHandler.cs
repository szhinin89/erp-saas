using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetTransferenciasList;

public sealed class GetTransferenciasListQueryHandler
    : IRequestHandler<GetTransferenciasListQuery, Result<TransferenciasPagedResult>>
{
    private readonly IStockTransferRepository _repo;
    private readonly ICurrentTenant           _currentTenant;

    public GetTransferenciasListQueryHandler(
        IStockTransferRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TransferenciasPagedResult>> Handle(
        GetTransferenciasListQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize   = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await _repo.GetPagedAsync(
            _currentTenant.TenantId,
            pageNumber, pageSize,
            query.SourceWarehouseId, query.TargetWarehouseId,
            query.Status, query.DateFrom, query.DateTo,
            ct);

        var dtos = items.Select(ToDto).ToList();
        return Result<TransferenciasPagedResult>.Success(
            new TransferenciasPagedResult(dtos, total, pageNumber, pageSize));
    }

    private static TransferenciaDto ToDto(StockTransfer t) => new(
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
