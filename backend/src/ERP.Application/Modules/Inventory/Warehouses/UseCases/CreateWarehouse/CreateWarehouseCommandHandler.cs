using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.CreateWarehouse;

public sealed class CreateWarehouseCommandHandler
    : IRequestHandler<CreateWarehouseCommand, Result<WarehouseListItemDto>>
{
    private readonly IWarehouseRepository _repo;
    private readonly IBranchRepository _branchRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateWarehouseCommandHandler(
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
        CreateWarehouseCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _company.CompanyId;

        var branch = await _branchRepo.GetByIdForCompanyAsync(
            tenantId,
            companyId,
            command.BranchId,
            cancellationToken
        );
        if (branch is null)
            return Result<WarehouseListItemDto>.ValidationFailure(
                "La sucursal no existe o no pertenece a esta empresa."
            );

        var code = $"WH-{Guid.NewGuid():N}"[..Warehouse.CodeMaxLen];

        var codeExists = await _repo.ExistsCodeAsync(
            tenantId,
            command.BranchId,
            code,
            null,
            cancellationToken
        );
        if (codeExists)
            return Result<WarehouseListItemDto>.Conflict(
                "Ya existe una bodega con ese código en la sucursal."
            );

        var entity = Warehouse.Create(
            tenantId: tenantId,
            branchId: command.BranchId,
            name: command.Name,
            code: code,
            storageType: command.StorageType,
            address: command.Address,
            phone: command.Phone,
            email: command.Email,
            manager: command.Manager,
            latitude: command.Latitude,
            longitude: command.Longitude,
            capacity: command.Capacity,
            dailyDispatchGoal: command.DailyDispatchGoal,
            createdBy: _user.UserId,
            companyId: companyId
        );

        await _repo.AddAsync(entity, cancellationToken);
        await _activity.AddAsync(
            UserActivity.Create(
                tenantId,
                _user.UserId,
                _user.Email,
                _user.FullName,
                module: "inventory",
                action: "warehouse.create",
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
