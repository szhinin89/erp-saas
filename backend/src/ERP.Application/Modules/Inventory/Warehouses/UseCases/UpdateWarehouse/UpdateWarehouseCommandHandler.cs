using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.UpdateWarehouse;

public sealed class UpdateWarehouseCommandHandler
    : IRequestHandler<UpdateWarehouseCommand, Result<WarehouseListItemDto>>
{
    private readonly IWarehouseRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public UpdateWarehouseCommandHandler(
        IWarehouseRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser user)
    {
        _repo          = repo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _user          = user;
    }

    public async Task<Result<WarehouseListItemDto>> Handle(
        UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;

        var entity = await _repo.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (entity is null)
            return Result<WarehouseListItemDto>.NotFound("Bodega no encontrada.");

        entity.Update(
            branchId:         command.BranchId,
            name:             command.Name,
            storageType:      command.StorageType,
            address:          command.Address,
            phone:            command.Phone,
            email:            command.Email,
            manager:          command.Manager,
            latitude:         command.Latitude,
            longitude:        command.Longitude,
            capacity:         command.Capacity,
            dailyDispatchGoal: command.DailyDispatchGoal,
            updatedBy:        _user.UserId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            _user.UserId,
            _user.Email,
            _user.FullName,
            module: "inventory",
            action: "warehouse.update",
            entityType: "Warehouse",
            entityId: entity.Id,
            description: entity.Name), cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<WarehouseListItemDto>.Success(GetWarehousesQueryHandler.ToDto(entity));
    }
}
