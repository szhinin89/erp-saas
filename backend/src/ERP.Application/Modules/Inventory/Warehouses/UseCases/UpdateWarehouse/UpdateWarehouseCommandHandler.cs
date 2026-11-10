using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.DTOs;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.GetWarehouses;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
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
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWarehouseCommandHandler(
        IWarehouseRepository repo,
        IBranchRepository branchRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentCompany company,
        ICurrentUser user,
        IUnitOfWork unitOfWork
    )
    {
        _repo = repo;
        _branchRepo = branchRepo;
        _activity = activity;
        _currentTenant = currentTenant;
        _company = company;
        _user = user;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WarehouseListItemDto>> Handle(
        UpdateWarehouseCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _company.CompanyId;

        var entity = await _repo.GetByIdForCompanyAsync(
            tenantId,
            companyId,
            command.Id,
            cancellationToken
        );
        if (entity is null)
            return Result<WarehouseListItemDto>.NotFound("Bodega no encontrada.");

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

        if (command.BranchId != entity.BranchId)
            return await ChangeBranchAsync(command, tenantId, companyId, cancellationToken);

        await ApplyAsync(entity, command, tenantId, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<WarehouseListItemDto>.Success(GetWarehousesQueryHandler.ToDto(entity));
    }

    /// <summary>
    /// WAREHOUSE-BRANCH-REASSIGNMENT-INTEGRITY-01: con la fila de la bodega bloqueada
    /// (<c>FOR UPDATE</c>) se reevalúan los bloqueos y se guarda en la misma transacción. El Kardex
    /// lee la sucursal de la bodega con <c>FOR SHARE</c>: un movimiento en curso hace esperar al
    /// cambio, que luego lo ve y lo rechaza; un movimiento posterior espera y registra la sucursal
    /// nueva.
    /// </summary>
    private async Task<Result<WarehouseListItemDto>> ChangeBranchAsync(
        UpdateWarehouseCommand command,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        Result<WarehouseListItemDto>? result = null;
        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var locked = await _repo.GetByIdForCompanyForUpdateAsync(tenantId, companyId, command.Id, ct);
                if (locked is null)
                {
                    result = Result<WarehouseListItemDto>.NotFound("Bodega no encontrada.");
                    return;
                }
                if (command.BranchId != locked.BranchId)
                {
                    var blocker = locked.IsMain
                        ? WarehouseBranchChangeBlocker.MainWarehouse
                        : await _repo.GetBranchChangeBlockerAsync(tenantId, companyId, locked.Id, ct);
                    if (blocker != WarehouseBranchChangeBlocker.None)
                    {
                        result = Result<WarehouseListItemDto>.ValidationFailure(
                            BranchChangeBlockedMessage(blocker)
                        );
                        return;
                    }
                }
                await ApplyAsync(locked, command, tenantId, ct);
                result = Result<WarehouseListItemDto>.Success(GetWarehousesQueryHandler.ToDto(locked));
            },
            cancellationToken
        );
        return result!;
    }

    private async Task ApplyAsync(
        Warehouse entity,
        UpdateWarehouseCommand command,
        Guid tenantId,
        CancellationToken cancellationToken
    )
    {
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
    }

    /// <summary>
    /// WAREHOUSE-BRANCH-REASSIGNMENT-INTEGRITY-01: la sucursal de una bodega solo cambia si no tiene
    /// existencias, historial ni referencias operativas; las existencias se trasladan con
    /// transferencias de inventario.
    /// </summary>
    internal static string BranchChangeBlockedMessage(WarehouseBranchChangeBlocker blocker)
    {
        var reason = blocker switch
        {
            WarehouseBranchChangeBlocker.MainWarehouse => "es la bodega principal de su sucursal",
            WarehouseBranchChangeBlocker.Stock => "tiene existencias",
            WarehouseBranchChangeBlocker.Movements => "tiene movimientos de inventario registrados",
            WarehouseBranchChangeBlocker.LotsSerialsOrReservations => "tiene lotes o series registrados",
            WarehouseBranchChangeBlocker.Documents => "está usada en documentos de compra, venta o inventario",
            WarehouseBranchChangeBlocker.DefaultSetting => "está configurada como bodega predeterminada",
            WarehouseBranchChangeBlocker.CashRegisterDefault => "es la bodega predeterminada de una caja",
            _ => "tiene información asociada",
        };
        return $"No se puede cambiar la sucursal de la bodega porque {reason}. Puede editar los "
            + "demás datos; para mover existencias use una transferencia de inventario.";
    }
}
