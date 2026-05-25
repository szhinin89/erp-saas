using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.ActualizarBodega;

public sealed class UpdateWarehouseCommandHandler
    : IRequestHandler<UpdateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;

    public UpdateWarehouseCommandHandler(
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

    public async Task<Result<WarehouseDto>> Handle(UpdateWarehouseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        var Warehouse = await _repo.GetByIdAsync(subscriberId, command.Id, ct);
        if (Warehouse is null)
            return Result<WarehouseDto>.Failure("Warehouse no encontrada.");

        if (await _repo.ExistsNameAsync(subscriberId, command.Name, command.Id, ct))
            return Result<WarehouseDto>.Failure($"Ya existe otra Warehouse con el nombre '{command.Name}' en este tenant.");

        Warehouse.Update(
            command.BranchId, command.Name,
            command.StorageType, command.Address, command.Phone, command.Email, command.Manager,
            command.Latitude, command.Longitude,
            command.Capacity, command.DailyDispatchGoal, userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "Warehouse.update",
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
