using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.UpdateInventoryAdjustmentReason;

public sealed class UpdateInventoryAdjustmentReasonCommandHandler
    : IRequestHandler<UpdateInventoryAdjustmentReasonCommand, Result<InventoryAdjustmentReasonDto>>
{
    private readonly IInventoryAdjustmentReasonRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public UpdateInventoryAdjustmentReasonCommandHandler(
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
        UpdateInventoryAdjustmentReasonCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var reason = await _repo.GetByIdAsync(tid, request.Id, ct);
        if (reason is null)
            return Result<InventoryAdjustmentReasonDto>.NotFound("Motivo no encontrado.");

        reason.Update(
            request.Name,
            request.AllowedMovementType,
            request.RequiresNotes,
            request.SortOrder,
            _user.UserId
        );

        await _repo.SaveChangesAsync(ct);

        return Result<InventoryAdjustmentReasonDto>.Success(StockAdjustmentMapper.ToDto(reason));
    }
}
