using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Modules.Inventario.UseCases.ActualizarBodega;

public sealed class UpdateBodegaCommandHandler
    : IRequestHandler<UpdateBodegaCommand, Result<BodegaDto>>
{
    private readonly IBodegaRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public UpdateBodegaCommandHandler(
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

    public async Task<Result<BodegaDto>> Handle(UpdateBodegaCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var bodega = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (bodega is null)
            return Result<BodegaDto>.Failure("Bodega no encontrada.");

        if (await _repo.ExistsNombreAsync(tenantId, command.Nombre, command.Id, ct))
            return Result<BodegaDto>.Failure($"Ya existe otra bodega con el nombre '{command.Nombre}' en este tenant.");

        bodega.Update(command.SucursalId, command.Nombre, command.Ubicacion, command.Encargado, userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "bodega.update",
            entityType: "Bodega", entityId: bodega.Id,
            description: bodega.Nombre), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BodegaDto>.Success(ToDto(bodega));
    }

    private static BodegaDto ToDto(Bodega b) =>
        new(b.Id, b.SucursalId, b.Nombre, b.Ubicacion, b.Encargado, b.IsActive);
}
