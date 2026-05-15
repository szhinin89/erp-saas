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
    private readonly IBodegaRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public EnableBodegaCommandHandler(
        IBodegaRepository repo,
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

        var bodega = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (bodega is null) return Result<BodegaDto>.Failure("Bodega no encontrada.");
        if (bodega.IsActive) return Result<BodegaDto>.Failure("La bodega ya está activa.");

        bodega.Enable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "bodega.enable",
            entityType: "Bodega", entityId: bodega.Id,
            description: bodega.Nombre), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BodegaDto>.Success(ToDto(bodega));
    }

    private static BodegaDto ToDto(Bodega b) =>
        new(b.Id, b.SucursalId, b.Nombre, b.Ubicacion, b.Encargado, b.IsActive);
}
