using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Common;

namespace ERP.Application.Configuration.UseCases.UpsertSubscriberBillingProfile;

public sealed class UpsertSubscriberBillingProfileCommandHandler
    : IRequestHandler<UpsertSubscriberBillingProfileCommand, Result<SubscriberBillingProfileDto>>
{
    private readonly ISubscriberBillingProfileRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpsertSubscriberBillingProfileCommandHandler(
        ISubscriberBillingProfileRepository repo,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
    }

    public async Task<Result<SubscriberBillingProfileDto>> Handle(
        UpsertSubscriberBillingProfileCommand command,
        CancellationToken ct)
    {
        var profile = await _repo.GetBySubscriberIdAsync(_currentSubscriber.SubscriberId, ct);
        if (profile is null)
        {
            profile = SubscriberBillingProfile.Create(
                _currentSubscriber.SubscriberId,
                command.IdentificationType,
                command.IdentificationNumber,
                command.LegalName,
                command.Address,
                _currentUser.UserId,
                tradeName:          command.TradeName,
                phone:              command.Phone,
                email:              command.Email,
                country:            command.Country,
                city:               command.City,
                requiresAccounting: command.RequiresAccounting,
                specialTaxpayer:    command.SpecialTaxpayer,
                logoBase64:         command.LogoBase64,
                footerText:         command.FooterText,
                receiptWidth:       command.ReceiptWidth,
                businessPartnerId:  command.BusinessPartnerId);
            await _repo.AddAsync(profile, ct);
        }
        else
        {
            profile.Update(
                command.IdentificationType,
                command.IdentificationNumber,
                command.LegalName,
                command.Address,
                _currentUser.UserId,
                tradeName:          command.TradeName,
                phone:              command.Phone,
                email:              command.Email,
                country:            command.Country,
                city:               command.City,
                requiresAccounting: command.RequiresAccounting,
                specialTaxpayer:    command.SpecialTaxpayer,
                logoBase64:         command.LogoBase64,
                footerText:         command.FooterText,
                receiptWidth:       command.ReceiptWidth,
                businessPartnerId:  command.BusinessPartnerId);
            await _repo.UpdateAsync(profile, ct);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<SubscriberBillingProfileDto>.Success(ToDto(profile));
    }

    private static SubscriberBillingProfileDto ToDto(SubscriberBillingProfile p) => new(
        p.Id, p.SubscriberId,
        p.IdentificationType, p.IdentificationNumber,
        p.LegalName, p.TradeName, p.Address, p.Phone, p.Email,
        p.Country, p.City,
        p.RequiresAccounting, p.SpecialTaxpayer,
        p.LogoBase64, p.FooterText, p.ReceiptWidth,
        p.BusinessPartnerId);
}