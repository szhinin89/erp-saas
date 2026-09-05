using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.EnableWarehouse;

public sealed class EnableWarehouseCommandHandler
    : IRequestHandler<EnableWarehouseCommand, Result<WarehouseListItemDto>>
{
    private readonly IWarehouseRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public EnableWarehouseCommandHandler(
        IWarehouseRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _repo = repo;
        _activity = activity;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<WarehouseListItemDto>> Handle(
        EnableWarehouseCommand request,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;

        var entity = await _repo.GetByIdForCompanyAsync(
            tenantId,
            _currentCompany.CompanyId,
            request.Id,
            cancellationToken
        );
        if (entity is null || entity.BranchId != _branch.BranchId)
            return Result<WarehouseListItemDto>.NotFound("Bodega no encontrada.");

        entity.Enable(_user.UserId);

        await _activity.AddAsync(
            UserActivity.Create(
                tenantId,
                _user.UserId,
                _user.Email,
                _user.FullName,
                module: "inventory",
                action: "warehouse.enable",
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
