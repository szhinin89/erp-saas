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
    private readonly ICurrentTenant _tenant;

    public GetStockAdjustmentByIdQueryHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        ICurrentTenant tenant
    )
    {
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _tenant = tenant;
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

        var reason = await _reasonRepo.GetByIdAsync(tid, adj.ReasonId, ct);
        return Result<StockAdjustmentDto>.Success(StockAdjustmentMapper.ToDto(adj, reason?.Name));
    }
}
