using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.UpdateWarehouse;

public sealed class UpdateWarehouseCommandHandler
    : IRequestHandler<UpdateWarehouseCommand, Result<WarehouseListItemDto>>
{
    private readonly IWarehouseRepository _repo;
    private readonly IBranchRepository _branchRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public UpdateWarehouseCommandHandler(
        IWarehouseRepository repo,
        IBranchRepository branchRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _branchRepo = branchRepo;
        _activity = activity;
        _currentTenant = currentTenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<WarehouseListItemDto>> Handle(
        UpdateWarehouseCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _company.CompanyId;

        var entity = await _repo.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (entity is null)
            return Result<WarehouseListItemDto>.NotFound("Bodega no encontrada.");

        var branch = await _branchRepo.GetByIdAsync(tenantId, command.BranchId, cancellationToken);
        if (branch is null || branch.CompanyId != companyId)
            return Result<WarehouseListItemDto>.ValidationFailure(
                "La sucursal no existe o no pertenece a esta empresa."
            );

        entity.Update(
            branchId: command.BranchId,
            name: command.Name,
            storageType: command.StorageType,
            address: command.Address,
            phone: command.Phone,
            email: command.Email,
            manager: command.Manager,
            latitude: command.Latitude,
            longitude: command.Longitude,
            capacity: command.Capacity,
            dailyDispatchGoal: command.DailyDispatchGoal,
            updatedBy: _user.UserId
        );

        await _activity.AddAsync(
            UserActivity.Create(
                tenantId,
                _user.UserId,
                _user.Email,
                _user.FullName,
                module: "inventory",
                action: "warehouse.update",
                entityType: "Warehouse",
                entityId: entity.Id,
                description: entity.Name
            ),
            cancellationToken
        );
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<WarehouseListItemDto>.Success(GetWarehousesQueryHandler.ToDto(entity));
    }
}
