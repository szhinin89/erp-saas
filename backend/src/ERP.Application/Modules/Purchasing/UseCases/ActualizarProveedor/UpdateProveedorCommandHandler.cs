using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ActualizarProveedor;

public sealed class UpdateProveedorCommandHandler
    : IRequestHandler<UpdateProveedorCommand, Result<ProveedorDto>>
{
    private readonly IProveedorRepository    _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public UpdateProveedorCommandHandler(
        IProveedorRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _user     = user;
    }

    public async Task<Result<ProveedorDto>> Handle(UpdateProveedorCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var proveedor = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (proveedor is null)
            return Result<ProveedorDto>.Failure("Proveedor no encontrado.");

        if (await _repo.ExistsRucAsync(tenantId, command.Ruc, command.Id, ct))
            return Result<ProveedorDto>.Failure($"Ya existe otro proveedor con el RUC '{command.Ruc}' en este tenant.");

        try
        {
            proveedor.Update(
                command.TipoPersona, command.RazonSocial, command.Ruc,
                command.Correo, command.Telefono, command.Direccion,
                command.CondicionPago, userId);
        }
        catch (ArgumentException ex)
        {
            return Result<ProveedorDto>.Failure(ex.Message);
        }

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "proveedor.update",
            entityType: "Proveedor", entityId: proveedor.Id,
            description: $"{proveedor.Ruc} — {proveedor.RazonSocial}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProveedorDto>.Success(ToDto(proveedor));
    }

    private static ProveedorDto ToDto(Proveedor p) =>
        new(p.Id, p.TipoPersona, p.RazonSocial, p.Ruc,
            p.Correo, p.Telefono, p.Direccion, p.CondicionPago, p.IsActive);
}
