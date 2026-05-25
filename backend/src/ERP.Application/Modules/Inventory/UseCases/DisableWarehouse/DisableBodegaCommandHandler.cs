using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.DeshabilitarBodega;

public sealed class DisableWarehouseCommandHandler
    : IRequestHandler<DisableWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;

    public DisableWarehouseCommandHandler(
        IWarehouseRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _subscriber = subscriber;
        _user     = user;
    }

    public async Task<Result<WarehouseDto>> Handle(DisableWarehouseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        var Warehouse = await _repo.GetByIdAsync(subscriberId, command.Id, ct);
        if (Warehouse is null) return Result<WarehouseDto>.Failure("Warehouse no encontrada.");
        if (!Warehouse.IsActive) return Result<WarehouseDto>.Failure("La Warehouse ya está deshabilitada.");

        Warehouse.Disable(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "Warehouse.disable",
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
