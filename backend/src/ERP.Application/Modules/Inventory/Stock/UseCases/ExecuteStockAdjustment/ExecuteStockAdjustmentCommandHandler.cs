using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-5) — un ajuste Draft creado válidamente para la bodega de la
/// Sucursal A podía ejecutarse (posteando el movimiento de Kardex real) desde una sesión activa en
/// la Sucursal B de la misma empresa, porque <c>ExecuteStockAdjustmentCommandHandler</c> nunca
/// resolvía la bodega del ajuste ni la comparaba contra la sucursal activa — mismo patrón de gap
/// ya corregido en FIX01 para CashSession (creación scoped, acción posterior sobre el recurso ya
/// creado sin re-chequeo).
/// INVENTORY-ADJUSTMENTS-02 — ahora postea UN StockMovement por línea (Ingreso → PositiveAdjust con
/// costo manual; Egreso → NegativeAdjust dejando que <c>AppendMovementAsync</c> resuelva el costo
/// promedio corrido), valida el motivo (activo, admite el MovementType, RequiresNotes) y valida
/// stock suficiente por línea de Egreso antes de mutar nada.
/// </summary>
public sealed class ExecuteStockAdjustmentCommandHandler
    : IRequestHandler<ExecuteStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public ExecuteStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IStockRepository stockRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant tenant,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _stockRepo = stockRepo;
        _warehouseRepo = warehouseRepo;
        _tenant = tenant;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        ExecuteStockAdjustmentCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        var adj = await _adjRepo.GetByIdAsync(tid, request.Id, ct);
        if (adj is null)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        var warehouse = await _warehouseRepo.GetByIdAsync(tid, adj.WarehouseId, ct);
        if (warehouse is null || warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        if (adj.Status != "Draft")
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"Solo ajustes en Draft pueden ejecutarse (actual: {adj.Status})."
            );

        if (adj.Lines.Count == 0)
            return Result<StockAdjustmentDto>.ValidationFailure("El ajuste no tiene líneas.");

        var reason = await _reasonRepo.GetByIdAsync(tid, adj.ReasonId, ct);
        if (reason is null)
            return Result<StockAdjustmentDto>.ValidationFailure("El motivo del ajuste no existe.");
        if (!reason.IsActive)
            return Result<StockAdjustmentDto>.ValidationFailure(
                "El motivo del ajuste está inactivo."
            );
        if (!reason.AllowsMovementType(adj.MovementType))
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"El motivo '{reason.Name}' no admite movimientos de tipo '{adj.MovementType}'."
            );
        if (reason.RequiresNotes && string.IsNullOrWhiteSpace(adj.Notes))
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"El motivo '{reason.Name}' requiere observaciones."
            );

        var isIngreso = adj.MovementType == StockAdjustment.MovementTypeIngreso;

        if (isIngreso)
        {
            var missingCost = adj.Lines.FirstOrDefault(l => l.UnitCostBase is null or <= 0);
            if (missingCost is not null)
                return Result<StockAdjustmentDto>.ValidationFailure(
                    $"El ítem '{missingCost.ItemName}' requiere un costo unitario mayor a cero para un ingreso."
                );
        }
        else
        {
            foreach (var line in adj.Lines)
            {
                var stock = await _stockRepo.GetStockAsync(tid, adj.WarehouseId, line.ItemId, ct);
                var available = stock?.Quantity ?? 0m;
                if (available < line.QuantityInBaseUom)
                    return Result<StockAdjustmentDto>.ValidationFailure(
                        $"Stock insuficiente para '{line.ItemName}': disponible {available}, solicitado {line.QuantityInBaseUom}."
                    );
            }
        }

        var uid = _user.UserId;
        var effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var movementType = isIngreso ? StockMovementType.PositiveAdjust : StockMovementType.NegativeAdjust;

        foreach (var line in adj.Lines)
        {
            var stockBefore = await _stockRepo.GetStockAsync(tid, adj.WarehouseId, line.ItemId, ct);
            var before = stockBefore?.Quantity ?? 0m;

            var signedQuantity = isIngreso ? line.QuantityInBaseUom : -line.QuantityInBaseUom;

            var movement = await _stockRepo.AppendMovementAsync(
                tid,
                adj.CompanyId,
                line.ItemId,
                adj.WarehouseId,
                movementType,
                signedQuantity,
                line.BaseUomCode,
                effectiveDate,
                adj.AdjustmentNumber,
                adj.Id,
                "StockAdjustment",
                uid,
                unitCost: isIngreso ? line.UnitCostBase : null,
                cancellationToken: ct,
                sourceDocLineId: line.Id
            );

            // Ingreso: el costo aplicado es el manual capturado en la línea (ya asignado).
            // Egreso: no se pasó unitCost — AppendMovementAsync consumió el costo promedio corrido
            // del Kardex; RunningAverageCost del movimiento resultante es la mejor aproximación
            // disponible sin volver a leer CurrentStock.AverageCost (prohibido para costeo).
            var resolvedCost = isIngreso ? line.UnitCostBase : movement.RunningAverageCost;

            line.ApplyExecutionResult(before, movement.ResultQuantity, resolvedCost);
        }

        adj.Execute(uid);
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason.Name));
    }
}
