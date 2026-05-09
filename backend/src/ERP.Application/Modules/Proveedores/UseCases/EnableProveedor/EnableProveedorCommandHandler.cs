using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Proveedores.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Proveedores.Entities;
using ERP.Domain.Proveedores.Interfaces;

namespace ERP.Application.Modules.Proveedores.UseCases.EnableProveedor;

public sealed class EnableProveedorCommandHandler
    : IRequestHandler<EnableProveedorCommand, Result<ProveedorDto>>
{
    private readonly IProveedorRepository    _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public EnableProveedorCommandHandler(
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

    public async Task<Result<ProveedorDto>> Handle(EnableProveedorCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var proveedor = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (proveedor is null) return Result<ProveedorDto>.Failure("Proveedor no encontrado.");
        if (proveedor.IsActive) return Result<ProveedorDto>.Failure("El proveedor ya está activo.");

        proveedor.Enable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "proveedor.enable",
            entityType: "Proveedor", entityId: proveedor.Id,
            description: $"{proveedor.Ruc} — {proveedor.RazonSocial}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProveedorDto>.Success(ToDto(proveedor));
    }

    private static ProveedorDto ToDto(Proveedor p) =>
        new(p.Id, p.TipoPersona, p.RazonSocial, p.Ruc,
            p.Correo, p.Telefono, p.Direccion, p.CondicionPago, p.IsActive);
}
