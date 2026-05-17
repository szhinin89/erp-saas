using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Domain.Modules.Logistics.Entities;
using ERP.Domain.Modules.Logistics.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.CreateCarrier;

public class CreateCarrierHandler : IRequestHandler<CreateCarrierCommand, Result<CarrierDto>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentTenant     _currentTenant;
    private readonly ICurrentUser       _currentUser;

    public CreateCarrierHandler(ICarrierRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<CarrierDto>> Handle(CreateCarrierCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var duplicate = await _repo.ExistsIdentificationAsync(tenantId, command.IdentificationNumber, excludeId: null, ct);
        if (duplicate)
            return Result<CarrierDto>.Failure("A carrier with this identification number already exists.");

        var carrier = Carrier.Create(
            tenantId,
            command.IdentificationType,
            command.IdentificationNumber,
            command.LegalName,
            command.LicensePlate,
            _currentUser.UserId,
            command.Phone,
            command.Email);

        await _repo.AddAsync(carrier, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<CarrierDto>.Success(ToDto(carrier));
    }

    private static CarrierDto ToDto(Carrier c) =>
        new(c.Id, c.IdentificationType, c.IdentificationNumber, c.LegalName, c.LicensePlate, c.Phone, c.Email, c.IsActive);
}
