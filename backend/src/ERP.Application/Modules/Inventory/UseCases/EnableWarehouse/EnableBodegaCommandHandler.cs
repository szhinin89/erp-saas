using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.HabilitarBodega;

public sealed class EnableWarehouseCommandHandler
    : IRequestHandler<EnableWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public EnableWarehouseCommandHandler(
        IWarehouseRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _user     = user;
    }

    public async Task<Result<WarehouseDto>> Handle(EnableWarehouseCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var Warehouse = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (Warehouse is null) return Result<WarehouseDto>.Failure("Warehouse no encontrada.");
        if (Warehouse.IsActive) return Result<WarehouseDto>.Failure("La Warehouse ya está activa.");

        Warehouse.Enable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "Warehouse.enable",
            entityType: "Warehouse", entityId: Warehouse.Id,
            description: Warehouse.Name), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<WarehouseDto>.Success(ToDto(Warehouse));
    }

    private static WarehouseDto ToDto(Warehouse w) =>
        new(w.Id, w.BranchId, w.Name, w.Code, w.StorageType,
            w.Address, w.Phone, w.Email, w.Manager,
            w.Latitude, w.Longitude, w.Capacity, w.DailyDispatchGoal, w.IsActive);
}
