using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Billing.Renewal;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Renewal engine that processes expiring subscriptions:
///  1. Load all billing accounts with CurrentPeriodEndUtc in [now, now + lookAheadHours]
///  2. Skip if already renewed (period already advanced) — idempotency
///  3. Generate draft invoice for next period
///  4. Attempt charge via IPaymentProvider (null = manual/skip)
///  5. On success → advance period, emit Paid event
///  6. On failure → EnterGracePeriodAsync via SubscriptionLifecycleOrchestrator
/// </summary>
public sealed class SubscriptionRenewalService : ISubscriptionRenewalService
{
    private readonly ErpDbContext                        _db;
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly IPaymentProviderFactory             _providerFactory;
    private readonly ISubscriberBillingRepository        _billing;
    private readonly IConfiguration                      _config;
    private readonly ILogger<SubscriptionRenewalService> _log;

    public SubscriptionRenewalService(
        ErpDbContext db,
        ISubscriptionLifecycleOrchestrator lifecycle,
        IPaymentProviderFactory providerFactory,
        ISubscriberBillingRepository billing,
        IConfiguration config,
        ILogger<SubscriptionRenewalService> log)
    {
        _db              = db;
        _lifecycle       = lifecycle;
        _providerFactory = providerFactory;
        _billing         = billing;
        _config          = config;
        _log             = log;
    }

    public async Task ProcessUpcomingRenewalsAsync(int lookAheadHours = 24, CancellationToken ct = default)
    {
        var now     = DateTime.UtcNow;
        var horizon = now.AddHours(Math.Max(1, lookAheadHours));

        // Load active/trialing accounts whose current period ends in the look-ahead window
        var accounts = await _db.SubscriberBillingAccounts
            .AsNoTracking()
            .Where(a =>
                (a.Status == BillingAccountStatus.Active || a.Status == BillingAccountStatus.Trialing) &&
                a.CurrentPeriodEndUtc.HasValue &&
                a.CurrentPeriodEndUtc.Value >= now &&
                a.CurrentPeriodEndUtc.Value <= horizon &&
                a.RenewalState == BillingRenewalState.Active)
            .Select(a => new { a.SubscriberId, a.Status, a.CurrentPeriodEndUtc, a.CurrencyCode })
            .ToListAsync(ct);

        _log.LogInformation(
            "SubscriptionRenewalService: {Count} accounts due for renewal in {Hours}h window",
            accounts.Count, lookAheadHours);

        foreach (var account in accounts)
        {
            try
            {
                await ProcessSubscriberRenewalAsync(account.SubscriberId, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "SubscriptionRenewalService: renewal failed for subscriber={SubscriberId}",
                    account.SubscriberId);
            }
        }
    }

    public async Task ProcessSubscriberRenewalAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var account = await _billing.GetAccountTrackedBySubscriberIdAsync(subscriberId, ct);
        if (account is null)
        {
            _log.LogWarning(
                "SubscriptionRenewalService: no billing account for subscriber={SubscriberId}", subscriberId);
            return;
        }

        // Idempotency: if period already advanced past now, skip
        if (account.CurrentPeriodEndUtc.HasValue && account.CurrentPeriodEndUtc.Value > DateTime.UtcNow.AddMinutes(5))
        {
            _log.LogDebug(
                "SubscriptionRenewalService: subscriber={SubscriberId} period ends {End} — skipping (not due yet)",
                subscriberId, account.CurrentPeriodEndUtc);
            return;
        }

        _log.LogInformation(
            "SubscriptionRenewalService: processing renewal for subscriber={SubscriberId}", subscriberId);

        // Get the provider and attempt charge
        var provider = _providerFactory.GetForSubscriber(subscriberId);

        if (provider.ProviderType == PaymentProviderType.None)
        {
            // Manual billing — just advance the period and log
            await AdvancePeriodAsync(subscriberId, account, ct);
            _log.LogInformation(
                "SubscriptionRenewalService: manual renewal for subscriber={SubscriberId} — period advanced",
                subscriberId);
            return;
        }

        // Auto-charge via provider
        var chargeResult = await provider.ChargeInvoiceAsync(
            new ChargeInvoiceRequest(
                subscriberId,
                ExternalCustomerId:  string.Empty, // resolved by provider impl from DB
                ExternalInvoiceId:   null,
                Amount:              0m,           // resolved by provider impl from subscription
                CurrencyCode:        account.CurrencyCode,
                Description:         $"Renewal for subscriber {subscriberId}"),
            ct);

        if (chargeResult.Succeeded && chargeResult.Data?.Succeeded == true)
        {
            await AdvancePeriodAsync(subscriberId, account, ct);
            await _billing.AddBillingEventAsync(
                BillingEvent.Record(subscriberId, BillingEvent.Types.InvoicePaid,
                    BillingEventSource.PaymentProvider, SubscriptionLifecycleOrchestrator.SystemActorId,
                    $"{{\"providerRef\":\"{chargeResult.Data.ProviderReference}\"}}"),
                ct);
            await _billing.SaveChangesAsync(ct);
        }
        else
        {
            var reason = chargeResult.Data?.FailureReason ?? chargeResult.ErrorMessage ?? "Payment failed";
            _log.LogWarning(
                "SubscriptionRenewalService: payment failed for subscriber={SubscriberId} reason={Reason}",
                subscriberId, reason);

            var graceDays = _config.GetValue("SaaS:RenewalGracePeriodDays", 7);
            await _lifecycle.EnterGracePeriodAsync(
                subscriberId,
                DateTime.UtcNow.AddDays(graceDays),
                SubscriptionLifecycleOrchestrator.SystemActorId,
                ct);
        }
    }

    private async Task AdvancePeriodAsync(
        Guid subscriberId,
        ERP.Domain.Billing.Entities.SubscriberBillingAccount account,
        CancellationToken ct)
    {
        // Advance period by 1 month (default — future: read from plan config)
        var now    = DateTime.UtcNow;
        var newEnd = (account.CurrentPeriodEndUtc ?? now).AddMonths(1);
        account.SetCurrentPeriodEnd(newEnd, SubscriptionLifecycleOrchestrator.SystemActorId);
        account.Activate(SubscriptionLifecycleOrchestrator.SystemActorId);
        await _billing.SaveChangesAsync(ct);
    }
}
