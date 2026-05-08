using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Customers.Entities;
using ERP.Domain.Customers.Interfaces;

namespace ERP.Application.Modules.Customers.UseCases.CreateCustomer;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public CreateCustomerCommandHandler(
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

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            return Result<CustomerDto>.Failure("Tenant no autenticado.");

        if (!_user.IsAuthenticated || _user.UserId == Guid.Empty)
            return Result<CustomerDto>.Failure("Usuario no autenticado.");

        var tenantId = _tenant.TenantId;
        var userId = _user.UserId;

        Customer entity;
        try
        {
            entity = Customer.Create(
                tenantId,
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

        if (await _repo.ExistsIdentificationAsync(tenantId, entity.IdentificationType, entity.IdentificationNumber, null, ct))
            return Result<CustomerDto>.Failure("Ya existe un cliente con el mismo tipo y número de identificación.");

        if (!command.IsActive)
            entity.Disable(userId);

        await _repo.AddAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _user.Email,
            _user.FullName,
            module: "ventas",
            action: "customer.create",
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
