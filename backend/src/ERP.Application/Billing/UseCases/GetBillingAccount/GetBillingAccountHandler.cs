using ERP.Application.Billing.DTOs;
using ERP.Application.Billing.Governance;
using ERP.Application.Common;
using ERP.Domain.Billing.Interfaces;
using MediatR;

namespace ERP.Application.Billing.UseCases.GetBillingAccount;

public sealed class GetBillingAccountHandler : IRequestHandler<GetBillingAccountQuery, Result<SubscriberBillingAccountDto>>
{
    private readonly ICurrentSubscriber _subscriber;
    private readonly ISubscriberBillingRepository _billing;
    private readonly IBillingGovernanceService _governance;

    public GetBillingAccountHandler(
        ICurrentSubscriber subscriber,
        ISubscriberBillingRepository billing,
        IBillingGovernanceService governance)
    {
        _subscriber = subscriber;
        _billing = billing;
        _governance = governance;
    }

    public async Task<Result<SubscriberBillingAccountDto>> Handle(GetBillingAccountQuery request, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        if (subscriberId == Guid.Empty)
            return Result<SubscriberBillingAccountDto>.Failure("Contexto de suscriptor no establecido.");

        var account = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        if (account is null)
            return Result<SubscriberBillingAccountDto>.Failure("Cuenta de facturación no configurada.");

        var state = await _governance.GetStateAsync(subscriberId, ct);

        return Result<SubscriberBillingAccountDto>.Success(new SubscriberBillingAccountDto(
            account.SubscriberId,
            account.Status.ToString(),
            account.RenewalState.ToString(),
            account.TrialState.ToString(),
            account.PrimaryProvider.ToString(),
            account.ExternalCustomerId,
            account.BillingEmail,
            account.CountryCode,
            account.CurrencyCode,
            account.DefaultPaymentMethodRef,
            account.TrialEndsAtUtc,
            account.GracePeriodEndsAtUtc,
            account.CurrentPeriodEndUtc,
            state.AllowsPlatformAccess));
    }
}
