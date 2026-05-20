using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.CrearBodega;

public sealed class CreateWarehouseCommandHandler
    : IRequestHandler<CreateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _tenant;
    private readonly ICurrentCompany           _company;
    private readonly ICurrentUser            _user;

    public CreateWarehouseCommandHandler(
        IWarehouseRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber tenant,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _company  = company;
        _user     = user;
    }

    public async Task<Result<WarehouseDto>> Handle(CreateWarehouseCommand command, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var userId   = _user.UserId;

        if (await _repo.ExistsNameAsync(subscriberId, command.Name, null, ct))
            return Result<WarehouseDto>.Failure($"Ya existe una Warehouse con el nombre '{command.Name}' en este tenant.");

        var code = $"WH-{DateTime.UtcNow.Year}-{Guid.NewGuid():N}"[..13];

        var companyId = _company.HasCompanyContext ? _company.CompanyId : (Guid?)null;
        var wh = Warehouse.Create(
            subscriberId, command.BranchId, command.Name,
            code, command.StorageType,
            command.Address, command.Phone, command.Email, command.Manager,
            command.Latitude, command.Longitude,
            command.Capacity, command.DailyDispatchGoal, userId,
            establishmentId: null,
            companyId: companyId);

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
