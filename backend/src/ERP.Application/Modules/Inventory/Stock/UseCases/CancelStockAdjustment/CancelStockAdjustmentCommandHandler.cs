using ERP.Application.Common;
using ERP.Application.Common.Services;
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
    private readonly ICompanyClock _companyClock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        IStockRepository stockRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant tenant,
        ICurrentBranch branch,
        ICurrentUser user,
        ICompanyClock companyClock,
        IUnitOfWork unitOfWork
    )
    {
        _unitOfWork = unitOfWork;
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _stockRepo = stockRepo;
        _warehouseRepo = warehouseRepo;
        _tenant = tenant;
        _branch = branch;
        _user = user;
        _companyClock = companyClock;
    }

    /// <summary>
    /// INVENTORY-V1-B2 — decisión y efectos en UNA transacción con el documento bloqueado
    /// (FOR UPDATE) antes de validar su estado: una solicitud concurrente espera y ve el estado ya
    /// aplicado (mismo patrón que CancelSalesInvoiceHandler). Orden de locks: documento → Kardex.
    /// </summary>
    public async Task<Result<StockAdjustmentDto>> Handle(
        CancelStockAdjustmentCommand request,
        CancellationToken ct
    )
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var result = await CancelAsync(request, ct);
            if (result.IsSuccess)
                await _unitOfWork.CommitAsync(ct);
            else
                await _unitOfWork.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Result<StockAdjustmentDto>> CancelAsync(
        CancelStockAdjustmentCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        var adj = await _adjRepo.GetByIdForUpdateAsync(tid, request.Id, ct);
        if (adj is null)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        var warehouse = await _warehouseRepo.GetByIdAsync(tid, adj.WarehouseId, ct);
        if (warehouse is null || warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        if (adj.Status != "Executed")
            return Result<StockAdjustmentDto>.ValidationFailure(
                $"Solo ajustes Ejecutados pueden anularse (actual: {adj.Status})."
            );

        // IL-4B — un documento de apertura (movimientos InitialBalance) no se revierte con la
        // anulación genérica: postearía NegativeAdjust con la fecha de hoy sobre un saldo al corte.
        // Su reverso, si se necesita, es un proceso específico de Carga Inicial.
        var openingDocuments = await _stockRepo.GetSourceDocIdsWithMovementTypeAsync(
            tid, "StockAdjustment", [adj.Id], StockMovementType.InitialBalance, ct);
        if (openingDocuments.Contains(adj.Id))
            return Result<StockAdjustmentDto>.ValidationFailure(
                "Este documento es un saldo inicial de inventario (Carga Inicial) y no se puede anular "
                    + "desde Ajustes de Inventario."
            );

        var uid = _user.UserId;
        var effectiveDate = await _companyClock.TodayAsync(adj.CompanyId, tid, ct);
        var isIngreso = adj.MovementType == StockAdjustment.MovementTypeIngreso;
        // Reversa siempre en tipo contrario: un Ingreso posteó PositiveAdjust, su reversa es
        // NegativeAdjust (y viceversa) — nunca reutiliza el mismo tipo de movimiento original.
        var reversalType = isIngreso
            ? StockMovementType.NegativeAdjust
            : StockMovementType.PositiveAdjust;
        var reference = $"ANULACIÓN: {adj.AdjustmentNumber}";
        // INVENTORY-V1-B3: la reversa conserva el importe que registró el movimiento original.
        var originalType = isIngreso ? StockMovementType.PositiveAdjust : StockMovementType.NegativeAdjust;
        var originals = (await _stockRepo.GetMovementsByDocumentAsync(tid, adj.Id, "StockAdjustment", ct))
            .Where(m => m.MovementType == originalType && m.SourceDocLineId.HasValue)
            .ToDictionary(m => m.SourceDocLineId!.Value);

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
                sourceDocLineId: line.Id,
                totalCost: originals.GetValueOrDefault(line.Id)?.TotalCost
            );
        }

        adj.Cancel(request.Reason, uid);
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        var reason = await _reasonRepo.GetByIdAsync(tid, adj.ReasonId, ct);
        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason?.Name));
    }
}
