using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.UpdateStockAdjustment;

/// <summary>
/// INVENTORY-ADJUSTMENTS-02 — actualiza un ajuste en Draft (cabecera + reemplazo total de líneas,
/// mismo patrón "reemplazar-todo" que otros drafts multi-línea). Rechaza cualquier ajuste que no
/// esté en Draft. No toca stock.
/// </summary>
public sealed class UpdateStockAdjustmentCommandHandler
    : IRequestHandler<UpdateStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly StockAdjustmentLineResolver _lineResolver;

    public UpdateStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IWarehouseRepository warehouseRepo,
        IItemRepository itemRepo,
        ICurrentTenant tenant,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _warehouseRepo = warehouseRepo;
        _tenant = tenant;
        _branch = branch;
        _user = user;
        _lineResolver = new StockAdjustmentLineResolver(itemRepo);
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        UpdateStockAdjustmentCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        var adj = await _adjRepo.GetByIdAsync(tid, request.Id, ct);
        if (adj is null)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        var currentWarehouse = await _warehouseRepo.GetByIdAsync(tid, adj.WarehouseId, ct);
        if (currentWarehouse is null || currentWarehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        if (adj.Status != "Draft")
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"Solo ajustes en Draft pueden editarse (actual: {adj.Status})."
            );

        var newWarehouse = await _warehouseRepo.GetByIdAsync(tid, request.WarehouseId, ct);
        if (newWarehouse is null)
            return Result<StockAdjustmentDto>.ValidationFailure("La bodega seleccionada no existe.");
        if (newWarehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.ValidationFailure(
                "La bodega seleccionada no pertenece a la sucursal activa."
            );

        var reason = await _reasonRepo.GetByIdAsync(tid, request.ReasonId, ct);
        if (reason is null)
            return Result<StockAdjustmentDto>.ValidationFailure("El motivo seleccionado no existe.");

        var lineResult = await _lineResolver.ResolveAsync(tid, adj.CompanyId, request.Lines, ct);
        if (!lineResult.IsSuccess)
            return Result<StockAdjustmentDto>.ValidationFailure(lineResult.Error!);

        adj.UpdateHeader(
            request.WarehouseId,
            request.WarehouseName,
            request.MovementType,
            request.ReasonId,
            request.Notes,
            _user.UserId
        );
        adj.ReplaceLines(lineResult.Value!);

        await _adjRepo.SaveChangesAsync(ct);

        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason.Name));
    }
}
