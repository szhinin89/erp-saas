using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Modules.Inventario.UseCases.CrearBodega;

public sealed class CreateBodegaCommandHandler
    : IRequestHandler<CreateBodegaCommand, Result<BodegaDto>>
{
    private readonly IBodegaRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public CreateBodegaCommandHandler(
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

    public async Task<Result<BodegaDto>> Handle(CreateBodegaCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (await _repo.ExistsNombreAsync(tenantId, command.Nombre, null, ct))
            return Result<BodegaDto>.Failure($"Ya existe una bodega con el nombre '{command.Nombre}' en este tenant.");

        var bodega = Bodega.Create(
            tenantId, command.SucursalId, command.Nombre,
            command.Ubicacion, command.Encargado, userId);

        await _repo.AddAsync(bodega, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "inventario", action: "bodega.create",
            entityType: "Bodega", entityId: bodega.Id,
            description: bodega.Nombre), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<BodegaDto>.Success(ToDto(bodega));
    }

    private static BodegaDto ToDto(Bodega b) =>
        new(b.Id, b.SucursalId, b.Nombre, b.Ubicacion, b.Encargado, b.IsActive);
}
