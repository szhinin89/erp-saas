using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetStockAdjustment;

public sealed class GetStockAdjustmentByIdQueryHandler
    : IRequestHandler<GetStockAdjustmentByIdQuery, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;

    public GetStockAdjustmentByIdQueryHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant tenant,
        ICurrentBranch branch
    )
    {
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _warehouseRepo = warehouseRepo;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        GetStockAdjustmentByIdQuery request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var adj = await _adjRepo.GetByIdAsync(tid, request.Id, ct);
        if (adj is null)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        // ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — StockAdjustment no persiste BranchId propio
        // (se resuelve siempre vía Warehouse.BranchId, ver StockAdjustment.cs); mismo chequeo que
        // ya usan Execute/Cancel/UpdateStockAdjustmentCommandHandler para el recurso ya cargado.
        var warehouse = await _warehouseRepo.GetByIdAsync(tid, adj.WarehouseId, ct);
        if (warehouse is null || warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        var reason = await _reasonRepo.GetByIdAsync(tid, adj.ReasonId, ct);
        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason?.Name));
    }
}
