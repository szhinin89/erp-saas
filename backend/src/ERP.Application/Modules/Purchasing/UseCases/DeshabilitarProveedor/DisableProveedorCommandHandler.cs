using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.DeshabilitarProveedor;

public sealed class DisableProveedorCommandHandler
    : IRequestHandler<DisableProveedorCommand, Result<ProveedorDto>>
{
    private readonly ISupplierRepository    _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public DisableProveedorCommandHandler(
        ISupplierRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _user     = user;
    }

    public async Task<Result<ProveedorDto>> Handle(DisableProveedorCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var Supplier = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (Supplier is null) return Result<ProveedorDto>.Failure("Supplier no encontrado.");
        if (!Supplier.IsActive) return Result<ProveedorDto>.Failure("El Supplier ya está deshabilitado.");

        Supplier.Disable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "Supplier.disable",
            entityType: "Supplier", entityId: Supplier.Id,
            description: $"{Supplier.Ruc} — {Supplier.LegalName}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProveedorDto>.Success(ToDto(Supplier));
    }

    private static ProveedorDto ToDto(Supplier p) =>
        new(p.Id, p.PersonType, p.LegalName, p.Ruc,
            p.Email, p.Phone, p.Address, p.PaymentTerms, p.IsActive);
}
