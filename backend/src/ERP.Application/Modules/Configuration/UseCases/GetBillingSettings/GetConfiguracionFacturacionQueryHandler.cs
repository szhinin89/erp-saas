using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetBillingSettings;

public sealed class GetBillingSettingsQueryHandler
    : IRequestHandler<GetBillingSettingsQuery, Result<BillingSettingsDto?>>
{
    private readonly IBillingSettingsRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetBillingSettingsQueryHandler(
        IBillingSettingsRepository repo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<BillingSettingsDto?>> Handle(
        GetBillingSettingsQuery query, CancellationToken ct)
    {
        var profile = await _repo.GetBySubscriberIdAsync(_currentSubscriber.SubscriberId, ct);
        if (profile is null)
            return Result<BillingSettingsDto?>.Success(null);

        return Result<BillingSettingsDto?>.Success(new BillingSettingsDto(
            profile.Id, profile.SubscriberId,
            profile.IdentificationType, profile.IdentificationNumber,
            profile.LegalName, profile.TradeName, profile.Address,
            profile.Phone, profile.Email, profile.Country, profile.City,
            profile.RequiresAccounting, profile.SpecialTaxpayer,
            profile.LogoBase64, profile.FooterText, profile.ReceiptWidth,
            profile.BusinessPartnerId));
    }
}