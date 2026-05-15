using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetAjusteById;

public sealed class GetStockAdjustmentByIdQueryHandler
    : IRequestHandler<GetStockAdjustmentByIdQuery, Result<StockAdjustmentDto?>>
{
    private readonly IStockAdjustmentRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetStockAdjustmentByIdQueryHandler(
        IStockAdjustmentRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<StockAdjustmentDto?>> Handle(
        GetStockAdjustmentByIdQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var ajuste   = await _repo.GetByIdAsync(tenantId, query.AdjustmentId, ct);

        if (ajuste is null)
            return Result<StockAdjustmentDto?>.Success(null);

        return Result<StockAdjustmentDto?>.Success(new(
            ajuste.Id, ajuste.AdjustmentNumber,
            ajuste.WarehouseId, ajuste.WarehouseName,
            ajuste.ProductId, ajuste.ProductName,
            ajuste.AdjustmentQty, ajuste.AdjustmentType,
            ajuste.Reason, ajuste.Notes,
            ajuste.AdjustmentDate, ajuste.Status,
            ajuste.ExecutedAt, ajuste.ExecutedBy,
            ajuste.CreatedAt));
    }
}
