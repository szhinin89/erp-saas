using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;

/// <summary>
/// INVENTORY-ADJUSTMENTS-02 — anula un ajuste Ejecutado posteando movimientos INVERSOS (mismo
/// patrón que <c>CancelPurchaseUseCases</c>): nunca edita ni borra los StockMovement originales.
/// Ingreso originalmente posteado → reversa con NegativeAdjust; Egreso originalmente posteado →
/// reversa con PositiveAdjust. Pasa siempre un costo explícito en la reversa (igual que
/// CancelPurchaseUseCases) para que el costo promedio corrido no se re-derive de forma distinta al
/// consumo/ingreso original.
/// </summary>
public sealed class CancelStockAdjustmentCommandHandler
    : IRequestHandler<CancelStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IInventoryAdjustmentReasonRepository _reasonRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public CancelStockAdjustmentCommandHandler(
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
        CancelStockAdjustmentCommand request,
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

        if (adj.Status != "Executed")
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"Solo ajustes Ejecutados pueden anularse (actual: {adj.Status})."
            );

        var uid = _user.UserId;
        var effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var isIngreso = adj.MovementType == StockAdjustment.MovementTypeIngreso;
        // Reversa siempre en tipo contrario: un Ingreso posteó PositiveAdjust, su reversa es
        // NegativeAdjust (y viceversa) — nunca reutiliza el mismo tipo de movimiento original.
        var reversalType = isIngreso ? StockMovementType.NegativeAdjust : StockMovementType.PositiveAdjust;
        var reference = $"ANULACIÓN: {adj.AdjustmentNumber}";

        foreach (var line in adj.Lines)
        {
            var signedQuantity = isIngreso ? -line.QuantityInBaseUom : line.QuantityInBaseUom;

            await _stockRepo.AppendMovementAsync(
                tid,
                adj.CompanyId,
                line.ItemId,
                adj.WarehouseId,
                reversalType,
                signedQuantity,
                line.BaseUomCode,
                effectiveDate,
                reference,
                adj.Id,
                "StockAdjustment",
                uid,
                unitCost: line.UnitCostBase,
                cancellationToken: ct,
                sourceDocLineId: line.Id
            );
        }

        adj.Cancel(request.Reason, uid);
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        var reason = await _reasonRepo.GetByIdAsync(tid, adj.ReasonId, ct);
        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason?.Name));
    }
}
