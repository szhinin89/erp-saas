using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;

public sealed class CreateInventoryAdjustmentReasonCommandHandler
    : IRequestHandler<CreateInventoryAdjustmentReasonCommand, Result<InventoryAdjustmentReasonDto>>
{
    private readonly IInventoryAdjustmentReasonRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreateInventoryAdjustmentReasonCommandHandler(
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
        CreateInventoryAdjustmentReasonCommand request,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;

        var existing = await _repo.GetByCodeAsync(tid, request.Code, ct);
        if (existing is not null)
            return Result<InventoryAdjustmentReasonDto>.UniqueViolation(
                $"Ya existe un motivo con el código '{request.Code}'."
            );

        var reason = InventoryAdjustmentReason.Create(
            tid,
            request.CompanyId,
            request.Code,
            request.Name,
            request.AllowedMovementType,
            request.RequiresNotes,
            request.SortOrder,
            _user.UserId
        );

        await _repo.AddAsync(reason, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<InventoryAdjustmentReasonDto>.Success(StockAdjustmentMapper.ToDto(reason));
    }
}
