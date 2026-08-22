using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ListInventoryAdjustmentReasons;

public sealed class ListInventoryAdjustmentReasonsQueryHandler
    : IRequestHandler<
        ListInventoryAdjustmentReasonsQuery,
        Result<IReadOnlyList<InventoryAdjustmentReasonDto>>
    >
{
    private readonly IInventoryAdjustmentReasonRepository _repo;
    private readonly ICurrentTenant _tenant;

    public ListInventoryAdjustmentReasonsQueryHandler(
        IInventoryAdjustmentReasonRepository repo,
        ICurrentTenant tenant
    )
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<InventoryAdjustmentReasonDto>>> Handle(
        ListInventoryAdjustmentReasonsQuery request,
        CancellationToken ct
    )
    {
        var items = await _repo.ListAsync(
            _tenant.TenantId,
            request.CompanyId,
            request.IncludeInactive,
            ct
        );

        return Result<IReadOnlyList<InventoryAdjustmentReasonDto>>.Success(
            items.Select(StockAdjustmentMapper.ToDto).ToList()
        );
    }
}
