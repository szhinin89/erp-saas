using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetTransferenciaById;

public sealed class GetTransferenciaByIdQueryHandler
    : IRequestHandler<GetTransferenciaByIdQuery, Result<TransferenciaDetailDto?>>
{
    private readonly IStockTransferRepository _repo;
    private readonly ICurrentTenant           _currentTenant;

    public GetTransferenciaByIdQueryHandler(
        IStockTransferRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TransferenciaDetailDto?>> Handle(
        GetTransferenciaByIdQuery query, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(_currentTenant.TenantId, query.Id, ct);
        if (t is null)
            return Result<TransferenciaDetailDto?>.Success(null);

        var detalles = t.Lines.Select(d => new TransferenciaDetalleDto(
            d.Id, d.ProductId, d.Description, d.Quantity)).ToList();

        return Result<TransferenciaDetailDto?>.Success(new TransferenciaDetailDto(
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
