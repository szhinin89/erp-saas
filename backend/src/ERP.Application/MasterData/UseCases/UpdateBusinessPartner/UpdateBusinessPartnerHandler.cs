using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.MasterData.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

public sealed class UpdateBusinessPartnerHandler
    : IRequestHandler<UpdateBusinessPartnerCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _repo;
    private readonly ICurrentUser _currentUser;
    private readonly IDatabaseExceptionTranslator _dbExceptions;
    private readonly ILogger<UpdateBusinessPartnerHandler> _logger;

    public UpdateBusinessPartnerHandler(
        IBusinessPartnerRepository repo,
        ICurrentUser currentUser,
        IDatabaseExceptionTranslator dbExceptions,
        ILogger<UpdateBusinessPartnerHandler> logger)
    {
        _repo = repo;
        _currentUser = currentUser;
        _dbExceptions = dbExceptions;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateBusinessPartnerCommand command, CancellationToken ct)
    {
        var bp = await _repo.GetByIdAsync(command.Id, ct);
        if (bp is null)
            return Result<bool>.Failure("BusinessPartner no encontrado.");

        var userId = _currentUser.UserId;

        var identificationChanged =
            bp.Identification.Type   != command.IdentificationType ||
            bp.Identification.Number != command.IdentificationNumber;

        if (identificationChanged)
        {
            var duplicate = await _repo.ExistsByIdentificationAsync(
                command.IdentificationType, command.IdentificationNumber, excludeId: command.Id, ct: ct);
            if (duplicate)
                return Result<bool>.ValidationFailure(
                    $"Ya existe un BusinessPartner con {command.IdentificationType} {command.IdentificationNumber}.");

            try { bp.UpdateIdentification(command.IdentificationType, command.IdentificationNumber, userId); }
            catch (ArgumentException ex) { return Result<bool>.ValidationFailure(ex.Message); }
        }

        try { bp.UpdateProfile(command.LegalName, command.TradeName, command.LegalRepresentativeName, command.Email, command.Phone, command.CountryCode, userId); }
        catch (ArgumentException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        try
        {
            await _repo.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex) when (_dbExceptions.TryGetUniqueViolation(ex, out var violation))
        {
            UniqueViolationLogger.LogUniqueViolation(
                _logger,
                nameof(UpdateBusinessPartnerHandler),
                violation,
                Guid.Empty,
                Guid.Empty);

            return Result<bool>.Conflict(
                $"Ya existe un BusinessPartner con {command.IdentificationType} {command.IdentificationNumber}.");
        }
    }
}
