using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.CreateBusinessPartner;

public sealed class CreateBusinessPartnerHandler
    : IRequestHandler<CreateBusinessPartnerCommand, Result<BusinessPartnerDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICustomerProfileRepository _cpRepo;
    private readonly ISupplierProfileRepository _spRepo;
    private readonly ICurrentSubscriber         _currentSubscriber;
    private readonly ICurrentUser               _currentUser;

    public CreateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        ICustomerProfileRepository cpRepo,
        ISupplierProfileRepository spRepo,
        ICurrentSubscriber         currentSubscriber,
        ICurrentUser               currentUser)
    {
        _bpRepo            = bpRepo;
        _cpRepo            = cpRepo;
        _spRepo            = spRepo;
        _currentSubscriber = currentSubscriber;
        _currentUser       = currentUser;
    }

    public async Task<Result<BusinessPartnerDto>> Handle(
        CreateBusinessPartnerCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<BusinessPartnerDto>.Failure("Contexto de suscriptor no establecido.");

        var userId = _currentUser.UserId;

        // Unicidad: mismo tipo+número por subscriber
        var duplicate = await _bpRepo.ExistsByIdentificationAsync(
            command.IdentificationType, command.IdentificationNumber, ct: ct);
        if (duplicate)
            return Result<BusinessPartnerDto>.Failure(
                $"Ya existe un BusinessPartner con {command.IdentificationType} {command.IdentificationNumber} en este suscriptor.");

        BusinessPartner bp;
        try
        {
            bp = BusinessPartner.Create(
                subscriberId,
                command.IdentificationType,
                command.IdentificationNumber,
                command.LegalName,
                userId,
                command.TradeName,
                command.Email,
                command.Phone,
                command.CountryCode);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerDto>.Failure(ex.Message);
        }

        await _bpRepo.AddAsync(bp, ct);

        if (command.AsCustomer)
        {
            var cp = CustomerProfile.Create(subscriberId, bp.Id, userId);
            await _cpRepo.AddAsync(cp, ct);
        }

        if (command.AsSupplier)
        {
            var sp = SupplierProfile.Create(subscriberId, bp.Id, userId);
            await _spRepo.AddAsync(sp, ct);
        }

        await _bpRepo.SaveChangesAsync(ct);

        return Result<BusinessPartnerDto>.Success(BusinessPartnerDto.From(bp));
    }
}
