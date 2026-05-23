using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpSettings;

public sealed class UpsertCompanyBpSettingsHandler
    : IRequestHandler<UpsertCompanyBpSettingsCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICompanyBpSettingsRepository _settingsRepo;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;
    private readonly IDatabaseExceptionTranslator _dbExceptions;
    private readonly ILogger<UpsertCompanyBpSettingsHandler> _logger;

    public UpsertCompanyBpSettingsHandler(
        IBusinessPartnerRepository bpRepo,
        ICompanyBpSettingsRepository settingsRepo,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user,
        IDatabaseExceptionTranslator dbExceptions,
        ILogger<UpsertCompanyBpSettingsHandler> logger)
    {
        _bpRepo = bpRepo;
        _settingsRepo = settingsRepo;
        _subscriber = subscriber;
        _company = company;
        _user = user;
        _dbExceptions = dbExceptions;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        UpsertCompanyBpSettingsCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId = _company.CompanyId;
        var userId = _user.UserId;

        if (companyId == Guid.Empty)
            return Result<bool>.Failure("Se requiere empresa activa en el contexto de sesión.");

        var bp = await _bpRepo.GetByIdAsync(command.BusinessPartnerId, ct);
        if (bp is null)
            return Result<bool>.Failure("BusinessPartner no encontrado.");

        var existing = await _settingsRepo.GetAsync(companyId, command.BusinessPartnerId, ct);

        try
        {
            if (existing is null)
            {
                var settings = CompanyBusinessPartnerSettings.Create(
                    subscriberId, companyId, command.BusinessPartnerId, userId,
                    command.CreditLimit, command.PaymentDays, command.IsBlocked);
                await _settingsRepo.AddAsync(settings, ct);
            }
            else
            {
                existing.Update(command.CreditLimit, command.PaymentDays, command.IsBlocked,
                    existing.PriceListId, userId);
            }

            await _settingsRepo.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex) when (_dbExceptions.TryGetUniqueViolation(ex, out var violation))
        {
            UniqueViolationLogger.LogUniqueViolation(
                _logger,
                nameof(UpsertCompanyBpSettingsHandler),
                violation,
                subscriberId,
                companyId);

            return Result<bool>.Conflict(
                "Ya existen condiciones comerciales para este BusinessPartner en la empresa activa. " +
                "Recargue y vuelva a intentar.");
        }
    }
}
