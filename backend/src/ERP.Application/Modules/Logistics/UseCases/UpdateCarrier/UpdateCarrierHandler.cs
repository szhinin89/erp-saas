using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Domain.Modules.Logistics.Entities;
using ERP.Domain.Modules.Logistics.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.UpdateCarrier;

public class UpdateCarrierHandler : IRequestHandler<UpdateCarrierCommand, Result<CarrierDto>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentSubscriber     _currentSubscriber;
    private readonly ICurrentUser       _currentUser;

    public UpdateCarrierHandler(ICarrierRepository repo, ICurrentSubscriber currentSubscriber, ICurrentUser currentUser)
    {
        _repo          = repo;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<CarrierDto>> Handle(UpdateCarrierCommand command, CancellationToken ct)
    {
        var carrier = await _repo.GetByIdAsync(command.CarrierId, ct);
        if (carrier is null || carrier.SubscriberId != _currentSubscriber.SubscriberId)
            return Result<CarrierDto>.Failure("Carrier not found.");

        var duplicate = await _repo.ExistsIdentificationAsync(
            _currentSubscriber.SubscriberId, command.IdentificationNumber, excludeId: command.CarrierId, ct);
        if (duplicate)
            return Result<CarrierDto>.Failure("Another carrier with this identification number already exists.");

        carrier.Update(
            command.IdentificationType,
            command.IdentificationNumber,
            command.LegalName,
            command.LicensePlate,
            _currentUser.UserId,
            command.Phone,
            command.Email);

        await _repo.SaveChangesAsync(ct);
        return Result<CarrierDto>.Success(ToDto(carrier));
    }

    private static CarrierDto ToDto(Carrier c) =>
        new(c.Id, c.IdentificationType, c.IdentificationNumber, c.LegalName, c.LicensePlate, c.Phone, c.Email, c.IsActive);
}
