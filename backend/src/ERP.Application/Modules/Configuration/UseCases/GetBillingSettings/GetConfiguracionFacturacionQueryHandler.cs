using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetSubscriberBillingProfile;

public sealed class GetSubscriberBillingProfileQueryHandler
    : IRequestHandler<GetSubscriberBillingProfileQuery, Result<SubscriberBillingProfileDto?>>
{
    private readonly ISubscriberBillingProfileRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetSubscriberBillingProfileQueryHandler(
        ISubscriberBillingProfileRepository repo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<SubscriberBillingProfileDto?>> Handle(
        GetSubscriberBillingProfileQuery query, CancellationToken ct)
    {
        var profile = await _repo.GetBySubscriberIdAsync(_currentSubscriber.SubscriberId, ct);
        if (profile is null)
            return Result<SubscriberBillingProfileDto?>.Success(null);

        return Result<SubscriberBillingProfileDto?>.Success(new SubscriberBillingProfileDto(
            profile.Id, profile.SubscriberId,
            profile.IdentificationType, profile.IdentificationNumber,
            profile.LegalName, profile.TradeName, profile.Address,
            profile.Phone, profile.Email, profile.Country, profile.City,
            profile.RequiresAccounting, profile.SpecialTaxpayer,
            profile.LogoBase64, profile.FooterText, profile.ReceiptWidth,
            profile.BusinessPartnerId));
    }
}