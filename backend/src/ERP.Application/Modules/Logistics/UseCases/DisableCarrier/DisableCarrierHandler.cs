using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using ERP.Domain.Modules.Logistics.Entities;
using ERP.Domain.Modules.Logistics.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.DisableCarrier;

public class DisableCarrierHandler : IRequestHandler<DisableCarrierCommand, Result<CarrierDto>>
{
    private readonly ICarrierRepository _repo;
    private readonly ICurrentSubscriber     _currentSubscriber;
    private readonly ICurrentUser       _currentUser;

    public DisableCarrierHandler(ICarrierRepository repo, ICurrentSubscriber currentSubscriber, ICurrentUser currentUser)
    {
        _repo = repo; _currentSubscriber = currentSubscriber; _currentUser = currentUser;
    }

    public async Task<Result<CarrierDto>> Handle(DisableCarrierCommand command, CancellationToken ct)
    {
        var carrier = await _repo.GetByIdAsync(command.CarrierId, ct);
        if (carrier is null || carrier.SubscriberId != _currentSubscriber.SubscriberId)
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
    private readonly ICurrentSubscriber     _currentSubscriber;
    private readonly ICurrentUser       _currentUser;

    public EnableCarrierHandler(ICarrierRepository repo, ICurrentSubscriber currentSubscriber, ICurrentUser currentUser)
    {
        _repo = repo; _currentSubscriber = currentSubscriber; _currentUser = currentUser;
    }

    public async Task<Result<CarrierDto>> Handle(EnableCarrierCommand command, CancellationToken ct)
    {
        var carrier = await _repo.GetByIdAsync(command.CarrierId, ct);
        if (carrier is null || carrier.SubscriberId != _currentSubscriber.SubscriberId)
            return Result<CarrierDto>.Failure("Carrier not found.");

        carrier.Enable(_currentUser.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<CarrierDto>.Success(ToDto(carrier));
    }

    private static CarrierDto ToDto(Carrier c) =>
        new(c.Id, c.IdentificationType, c.IdentificationNumber, c.LegalName, c.LicensePlate, c.Phone, c.Email, c.IsActive);
}
