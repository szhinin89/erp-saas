using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.MasterData.UseCases.CreateBusinessPartner;

public sealed class CreateBusinessPartnerHandler
    : IRequestHandler<CreateBusinessPartnerCommand, Result<BusinessPartnerDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICustomerProfileRepository _cpRepo;
    private readonly ISupplierProfileRepository _spRepo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly IDatabaseExceptionTranslator _dbExceptions;
    private readonly IBusinessPartnerOperationalLinkEnricher _linkEnricher;
    private readonly ILogger<CreateBusinessPartnerHandler> _logger;

    public CreateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        ICustomerProfileRepository cpRepo,
        ISupplierProfileRepository spRepo,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IDatabaseExceptionTranslator dbExceptions,
        IBusinessPartnerOperationalLinkEnricher linkEnricher,
        ILogger<CreateBusinessPartnerHandler> logger)
    {
        _bpRepo = bpRepo;
        _cpRepo = cpRepo;
        _spRepo = spRepo;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _dbExceptions = dbExceptions;
        _linkEnricher = linkEnricher;
        _logger = logger;
    }

    public async Task<Result<BusinessPartnerDto>> Handle(
        CreateBusinessPartnerCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<BusinessPartnerDto>.Failure("Contexto de suscriptor no establecido.");

        var userId = _currentUser.UserId;

        var duplicate = await _bpRepo.ExistsByIdentificationAsync(
            command.IdentificationType, command.IdentificationNumber, ct: ct);
        if (duplicate)
            return Result<BusinessPartnerDto>.ValidationFailure(
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
                command.LegalRepresentativeName,
                command.Email,
                command.Phone,
                command.CountryCode);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerDto>.ValidationFailure(ex.Message);
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

        try
        {
            await _bpRepo.SaveChangesAsync(ct);
            var enriched = await _linkEnricher.EnrichAsync(bp, ct);
            return Result<BusinessPartnerDto>.Success(enriched);
        }
        catch (Exception ex) when (_dbExceptions.TryGetUniqueViolation(ex, out var violation))
        {
            UniqueViolationLogger.LogUniqueViolation(
                _logger,
                nameof(CreateBusinessPartnerHandler),
                violation,
                subscriberId,
                Guid.Empty);

            return Result<BusinessPartnerDto>.Conflict(
                $"Ya existe un BusinessPartner con {command.IdentificationType} {command.IdentificationNumber} en este suscriptor.");
        }
    }
}
