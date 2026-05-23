using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpSettings;

public sealed class UpsertCompanyBpSettingsHandler
    : IRequestHandler<UpsertCompanyBpSettingsCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository   _bpRepo;
    private readonly ICompanyBpSettingsRepository _settingsRepo;
    private readonly ICurrentSubscriber           _subscriber;
    private readonly ICurrentCompany              _company;
    private readonly ICurrentUser                 _user;

    public UpsertCompanyBpSettingsHandler(
        IBusinessPartnerRepository   bpRepo,
        ICompanyBpSettingsRepository settingsRepo,
        ICurrentSubscriber           subscriber,
        ICurrentCompany              company,
        ICurrentUser                 user)
    {
        _bpRepo       = bpRepo;
        _settingsRepo = settingsRepo;
        _subscriber   = subscriber;
        _company      = company;
        _user         = user;
    }

    public async Task<Result<bool>> Handle(
        UpsertCompanyBpSettingsCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId    = _company.CompanyId;
        var userId       = _user.UserId;

        if (companyId == Guid.Empty)
            return Result<bool>.Failure("Se requiere empresa activa en el contexto de sesión.");

        var bp = await _bpRepo.GetByIdAsync(command.BusinessPartnerId, ct);
        if (bp is null)
            return Result<bool>.Failure("BusinessPartner no encontrado.");

        var existing = await _settingsRepo.GetAsync(companyId, command.BusinessPartnerId, ct);

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
}
