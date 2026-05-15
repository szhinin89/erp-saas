using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases.ActualizarCliente;

public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public UpdateCustomerCommandHandler(
        ICustomerRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo = repo;
        _activity = activity;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand command, CancellationToken ct)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            return Result<CustomerDto>.Failure("Tenant no autenticado.");

        if (!_user.IsAuthenticated || _user.UserId == Guid.Empty)
            return Result<CustomerDto>.Failure("Usuario no autenticado.");

        var emailErr = CustomerInputValidation.ValidateOptionalEmail(command.Email);
        if (emailErr is not null)
            return Result<CustomerDto>.Failure(emailErr);

        var tenantId = _tenant.TenantId;
        var userId = _user.UserId;

        var entity = await _repo.GetByIdAsync(tenantId, command.Id, ct);
        if (entity is null)
            return Result<CustomerDto>.Failure("Cliente no encontrado.");

        try
        {
            entity.Update(
                command.IdentificationType,
                command.IdentificationNumber,
                command.LegalName,
                command.TradeName,
                command.AddressLine,
                command.Phone,
                command.Email,
                command.Notes,
                userId);
        }
        catch (ArgumentException ex)
        {
            return Result<CustomerDto>.Failure(ex.Message);
        }

        if (await _repo.ExistsIdentificationAsync(
                tenantId,
                entity.IdentificationType,
                entity.IdentificationNumber,
                entity.Id,
                ct))
            return Result<CustomerDto>.Failure("Ya existe otro cliente con el mismo tipo y número de identificación.");

        if (command.IsActive && !entity.IsActive)
            entity.Enable(userId);
        else if (!command.IsActive && entity.IsActive)
            entity.Disable(userId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _user.Email,
            _user.FullName,
            module: "ventas",
            action: "customer.update",
            entityType: "Customer",
            entityId: entity.Id,
            description: $"{entity.IdentificationType} {entity.IdentificationNumber} — {entity.LegalName}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<CustomerDto>.Success(new CustomerDto(
            entity.Id,
            entity.IdentificationType,
            entity.IdentificationNumber,
            entity.LegalName,
            entity.TradeName,
            entity.AddressLine,
            entity.Phone,
            entity.Email,
            entity.Notes,
            entity.IsActive));
    }
}
