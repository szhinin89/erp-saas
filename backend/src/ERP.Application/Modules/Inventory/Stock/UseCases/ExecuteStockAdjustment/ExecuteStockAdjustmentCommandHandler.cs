using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
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
/// </summary>
public sealed class ExecuteStockAdjustmentCommandHandler
    : IRequestHandler<ExecuteStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IStockRepository _stockRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public ExecuteStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IStockRepository stockRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant tenant,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _adjRepo = adjRepo;
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
        var adj = await _adjRepo.GetByIdAsync(_tenant.TenantId, request.Id, ct);
        if (adj is null)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        var warehouse = await _warehouseRepo.GetByIdAsync(_tenant.TenantId, adj.WarehouseId, ct);
        if (warehouse is null || warehouse.BranchId != _branch.BranchId)
            return Result<StockAdjustmentDto>.NotFound("Ajuste no encontrado.");

        if (adj.Status != "Draft")
            return Result<StockAdjustmentDto>.Failure(
                $"Solo ajustes en Draft pueden ejecutarse (actual: {adj.Status})."
            );

        var uid = _user.UserId;
        var tid = _tenant.TenantId;

        var movementType =
            adj.AdjustmentQty > 0
                ? StockMovementType.PositiveAdjust
                : StockMovementType.NegativeAdjust;

        await _stockRepo.AppendMovementAsync(
            tid,
            adj.CompanyId,
            adj.ProductId,
            adj.WarehouseId,
            movementType,
            adj.AdjustmentQty,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            adj.AdjustmentNumber,
            adj.Id,
            "StockAdjustment",
            uid,
            cancellationToken: ct
        );

        adj.Execute(uid);
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        return Result<StockAdjustmentDto>.Success(
            CreateStockAdjustment.CreateStockAdjustmentCommandHandler.ToDto(adj)
        );
    }
}
