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
    private readonly ICurrentTenant _tenant;

    public ListStockAdjustmentsQueryHandler(
        IStockAdjustmentRepository adjRepo,
        IInventoryAdjustmentReasonRepository reasonRepo,
        ICurrentTenant tenant
    )
    {
        _adjRepo = adjRepo;
        _reasonRepo = reasonRepo;
        _tenant = tenant;
    }

    public async Task<Result<PagedResult<StockAdjustmentDto>>> Handle(
        ListStockAdjustmentsQuery request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
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
