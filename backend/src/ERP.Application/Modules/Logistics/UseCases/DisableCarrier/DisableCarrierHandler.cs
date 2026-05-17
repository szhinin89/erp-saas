using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Domain.Modules.Logistics.Entities;
using ERP.Domain.Modules.Logistics.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.DisableCarrier;

public class DisableCarrierHandler : IRequestHandler<DisableCarrierCommand, Result<CarrierDto>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentTenant     _currentTenant;
    private readonly ICurrentUser       _currentUser;

    public DisableCarrierHandler(ICarrierRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo; _currentTenant = currentTenant; _currentUser = currentUser;
    }

    public async Task<Result<CarrierDto>> Handle(DisableCarrierCommand command, CancellationToken ct)
    {
        var carrier = await _repo.GetByIdAsync(command.CarrierId, ct);
        if (carrier is null || carrier.TenantId != _currentTenant.TenantId)
            return Result<CarrierDto>.Failure("Carrier not found.");

        carrier.Disable(_currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<CarrierDto>.Success(ToDto(carrier));
    }

    private static CarrierDto ToDto(Carrier c) =>
        new(c.Id, c.IdentificationType, c.IdentificationNumber, c.LegalName, c.LicensePlate, c.Phone, c.Email, c.IsActive);
}

public class EnableCarrierHandler : IRequestHandler<EnableCarrierCommand, Result<CarrierDto>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentTenant     _currentTenant;
    private readonly ICurrentUser       _currentUser;

    public EnableCarrierHandler(ICarrierRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo; _currentTenant = currentTenant; _currentUser = currentUser;
    }

    public async Task<Result<CarrierDto>> Handle(EnableCarrierCommand command, CancellationToken ct)
    {
        var carrier = await _repo.GetByIdAsync(command.CarrierId, ct);
        if (carrier is null || carrier.TenantId != _currentTenant.TenantId)
            return Result<CarrierDto>.Failure("Carrier not found.");

        carrier.Enable(_currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<CarrierDto>.Success(ToDto(carrier));
    }

    private static CarrierDto ToDto(Carrier c) =>
        new(c.Id, c.IdentificationType, c.IdentificationNumber, c.LegalName, c.LicensePlate, c.Phone, c.Email, c.IsActive);
}
