using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.HabilitarBodega;

public sealed class EnableBodegaCommandHandler
    : IRequestHandler<EnableBodegaCommand, Result<BodegaDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public EnableBodegaCommandHandler(
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

    public async Task<Result<BodegaDto>> Handle(EnableBodegaCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var Warehouse = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (Warehouse is null) return Result<BodegaDto>.Failure("Warehouse no encontrada.");
        if (Warehouse.IsActive) return Result<BodegaDto>.Failure("La Warehouse ya está activa.");

        Warehouse.Enable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "Warehouse.enable",
            entityType: "Warehouse", entityId: Warehouse.Id,
            description: Warehouse.Name), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BodegaDto>.Success(ToDto(Warehouse));
    }

    private static BodegaDto ToDto(Warehouse b) =>
        new(b.Id, b.BranchId, b.Name, b.Address, b.Manager, b.IsActive);
}
