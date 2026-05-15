using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.CrearBodega;

public sealed class CreateBodegaCommandHandler
    : IRequestHandler<CreateBodegaCommand, Result<BodegaDto>>
{
    private readonly IWarehouseRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public CreateBodegaCommandHandler(
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

    public async Task<Result<BodegaDto>> Handle(CreateBodegaCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (await _repo.ExistsNameAsync(tenantId, command.Name, null, ct))
            return Result<BodegaDto>.Failure($"Ya existe una Warehouse con el nombre '{command.Name}' en este tenant.");

        var wh = Warehouse.Create(
            tenantId, command.BranchId, command.Name,
            command.Address, command.Manager, userId);

        await _repo.AddAsync(wh, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "Warehouse.create",
            entityType: "Warehouse", entityId: wh.Id,
            description: wh.Name), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BodegaDto>.Success(ToDto(wh));
    }

    private static BodegaDto ToDto(Warehouse b) =>
        new(b.Id, b.BranchId, b.Name, b.Address, b.Manager, b.IsActive);
}
