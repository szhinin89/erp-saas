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
    private readonly ISupplierRepository    _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public UpdateProveedorCommandHandler(
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

    public async Task<Result<ProveedorDto>> Handle(UpdateProveedorCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var Supplier = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (Supplier is null)
            return Result<ProveedorDto>.Failure("Supplier no encontrado.");

        if (await _repo.ExistsRucAsync(tenantId, command.Ruc, command.Id, ct))
            return Result<ProveedorDto>.Failure($"Ya existe otro Supplier con el RUC '{command.Ruc}' en este tenant.");

        try
        {
            Supplier.Update(
                command.PersonType, command.LegalName, command.Ruc,
                command.Email, command.Phone, command.Address,
                command.PaymentTerms, userId);
        }
        catch (ArgumentException ex)
        {
            return Result<ProveedorDto>.Failure(ex.Message);
        }

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "Supplier.update",
            entityType: "Supplier", entityId: Supplier.Id,
            description: $"{Supplier.Ruc} — {Supplier.LegalName}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProveedorDto>.Success(ToDto(Supplier));
    }

    private static ProveedorDto ToDto(Supplier p) =>
        new(p.Id, p.PersonType, p.LegalName, p.Ruc,
            p.Email, p.Phone, p.Address, p.PaymentTerms, p.IsActive);
}
