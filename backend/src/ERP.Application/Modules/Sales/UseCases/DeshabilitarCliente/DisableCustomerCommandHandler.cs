using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases.DeshabilitarCliente;

public sealed class DisableCustomerCommandHandler : IRequestHandler<DisableCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public DisableCustomerCommandHandler(
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

    public async Task<Result<CustomerDto>> Handle(DisableCustomerCommand request, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, request.Id, ct);
        if (entity is null)
            return Result<CustomerDto>.Failure("Cliente no encontrado.");

        try
        {
            entity.Disable(_user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<CustomerDto>.Failure(ex.Message);
        }

        await _activity.AddAsync(UserActivity.Create(
            _tenant.TenantId,
            _user.UserId,
            _user.Email,
            _user.FullName,
            module: "ventas",
            action: "customer.disable",
            entityType: "Customer",
            entityId: entity.Id,
            description: entity.LegalName), ct);
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
