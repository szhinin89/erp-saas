using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.CreateWarehouse;

public sealed class CreateWarehouseCommandHandler
    : IRequestHandler<CreateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentCompany           _company;
    private readonly ICurrentUser            _user;

    public CreateWarehouseCommandHandler(
        IWarehouseRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _subscriber = subscriber;
        _company  = company;
        _user     = user;
    }

    public async Task<Result<WarehouseDto>> Handle(CreateWarehouseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        if (await _repo.ExistsNameAsync(subscriberId, command.Name, null, ct))
            return Result<WarehouseDto>.Failure($"Ya existe una Warehouse con el nombre '{command.Name}' en este tenant.");

        var code = $"WH-{DateTime.UtcNow.Year}-{Guid.NewGuid():N}"[..13];

        if (!_company.HasCompanyContext)
            return Result<WarehouseDto>.Failure("Se requiere contexto de empresa para crear una bodega.");

        var wh = Warehouse.Create(
            subscriberId, command.BranchId, command.Name,
            code, command.StorageType,
            command.Address, command.Phone, command.Email, command.Manager,
            command.Latitude, command.Longitude,
            command.Capacity, command.DailyDispatchGoal, userId,
            companyId: _company.CompanyId);

        await _repo.AddAsync(wh, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "Warehouse.create",
            entityType: "Warehouse", entityId: wh.Id,
            description: wh.Name), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<WarehouseDto>.Success(ToDto(wh));
    }

    private static WarehouseDto ToDto(Warehouse w) =>
        new(w.Id, w.BranchId, w.Name, w.Code, w.StorageType,
            w.Address, w.Phone, w.Email, w.Manager,
            w.Latitude, w.Longitude, w.Capacity, w.DailyDispatchGoal, w.IsActive);
}
