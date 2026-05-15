using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearProveedor;

public sealed class CreateProveedorCommandHandler
    : IRequestHandler<CreateProveedorCommand, Result<ProveedorDto>>
{
    private readonly ISupplierRepository    _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public CreateProveedorCommandHandler(
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

    public async Task<Result<ProveedorDto>> Handle(CreateProveedorCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (await _repo.ExistsRucAsync(tenantId, command.Ruc, null, ct))
            return Result<ProveedorDto>.Failure($"Ya existe un Supplier con el RUC '{command.Ruc}' en este tenant.");

        Supplier Supplier;
        try
        {
            Supplier = Supplier.Create(
                tenantId, command.PersonType, command.LegalName, command.Ruc,
                command.Email, command.Phone, command.Address, command.PaymentTerms,
                userId);
        }
        catch (ArgumentException ex)
        {
            return Result<ProveedorDto>.Failure(ex.Message);
        }

        await _repo.AddAsync(Supplier, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "Supplier.create",
            entityType: "Supplier", entityId: Supplier.Id,
            description: $"{Supplier.Ruc} — {Supplier.LegalName}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProveedorDto>.Success(ToDto(Supplier));
    }

    private static ProveedorDto ToDto(Supplier p) =>
        new(p.Id, p.PersonType, p.LegalName, p.Ruc,
            p.Email, p.Phone, p.Address, p.PaymentTerms, p.IsActive);
}
