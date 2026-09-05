using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ListStockAdjustments;

public sealed class ListStockAdjustmentsQueryHandler
    : IRequestHandler<ListStockAdjustmentsQuery, Result<PagedResult<StockAdjustmentDto>>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;

    public ListStockAdjustmentsQueryHandler(
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

    public async Task<Result<PagedResult<StockAdjustmentDto>>> Handle(
        ListStockAdjustmentsQuery request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        // ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — el listado es branch-scoped (StockAdjustment
        // no tiene BranchId propio, se resuelve vía Warehouse.BranchId): si el caller filtró por
        // una bodega puntual, esa bodega debe pertenecer a la sucursal activa; si no filtró,
        // se restringe automáticamente a las bodegas de la sucursal activa (nunca "todas las
        // bodegas de la empresa", que filtraría ajustes de otras sucursales).
        if (request.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepo.GetByIdAsync(tid, request.WarehouseId.Value, ct);
            if (warehouse is null || warehouse.BranchId != _branch.BranchId)
                return Result<PagedResult<StockAdjustmentDto>>.ValidationFailure(
                    "La bodega seleccionada no pertenece a la sucursal activa."
                );
        }

        IReadOnlyCollection<Guid>? branchWarehouseIds = null;
        if (!request.WarehouseId.HasValue)
        {
            var branchWarehouses = await _warehouseRepo.GetAsync(
                tid,
                activeFilter: null,
                search: null,
                branchId: _branch.BranchId,
                ct
            );
            branchWarehouseIds = branchWarehouses.Select(w => w.Id).ToList();
        }

        var (items, total) = await _adjRepo.GetPagedAsync(
            tid,
            request.PageNumber,
            request.PageSize,
            request.WarehouseId,
            request.Status,
            request.ReasonId,
            request.MovementType,
            request.StartDate,
            request.EndDate,
            branchWarehouseIds,
            ct
        );

        var reasons = await _reasonRepo.ListAsync(tid, null, includeInactive: true, ct);
        var reasonNames = reasons.ToDictionary(r => r.Id, r => r.Name);

        var dtos = items
            .Select(a =>
                StockAdjustmentMapper.ToDto(
                    a,
                    reasonNames.TryGetValue(a.ReasonId, out var name) ? name : null
                )
            )
            .ToList();

        return Result<PagedResult<StockAdjustmentDto>>.Success(
            new PagedResult<StockAdjustmentDto>(dtos, request.PageNumber, request.PageSize, total)
        );
    }
}
