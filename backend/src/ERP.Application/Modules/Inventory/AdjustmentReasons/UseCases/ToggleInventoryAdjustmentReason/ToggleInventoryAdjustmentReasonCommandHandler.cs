using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ToggleInventoryAdjustmentReason;

public sealed class ToggleInventoryAdjustmentReasonCommandHandler
    : IRequestHandler<ToggleInventoryAdjustmentReasonCommand, Result<InventoryAdjustmentReasonDto>>
{
    private readonly IInventoryAdjustmentReasonRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public ToggleInventoryAdjustmentReasonCommandHandler(
        IInventoryAdjustmentReasonRepository repo,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<InventoryAdjustmentReasonDto>> Handle(
        ToggleInventoryAdjustmentReasonCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var reason = await _repo.GetByIdAsync(tid, request.Id, ct);
        if (reason is null)
            return Result<InventoryAdjustmentReasonDto>.NotFound("Motivo no encontrado.");

        try
        {
            if (request.Activate)
                reason.Enable(_user.UserId);
            else
                reason.Disable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<InventoryAdjustmentReasonDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return Result<InventoryAdjustmentReasonDto>.Success(StockAdjustmentMapper.ToDto(reason));
    }
}
