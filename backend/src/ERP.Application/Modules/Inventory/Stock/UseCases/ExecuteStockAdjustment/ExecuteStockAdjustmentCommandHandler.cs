using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;

public sealed class ExecuteStockAdjustmentCommandHandler
    : IRequestHandler<ExecuteStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public ExecuteStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        IStockRepository stockRepo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _adjRepo = adjRepo;
        _stockRepo = stockRepo;
        _tenant = tenant;
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
