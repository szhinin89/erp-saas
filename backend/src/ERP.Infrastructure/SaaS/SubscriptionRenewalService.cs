using System.Diagnostics;
using ERP.Application.Billing.Invoices;
using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Billing.Renewal;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Renewal engine — processes expiring subscriptions hourly.
///
/// IDEMPOTENCY: PostgreSQL advisory lock prevents double-renewal per subscriber.
/// INVOICE: Creates SaasBillingInvoice before charging.
/// PAYMENT ATTEMPT: Persists BillingPaymentAttempt for every charge attempt.
/// GRACE PERIOD: Enters grace period via ISubscriptionLifecycleOrchestrator on failure.
/// </summary>
public sealed class SubscriptionRenewalService : ISubscriptionRenewalService
{
    private readonly ErpDbContext                        _db;
    private readonly ISubscriptionLifecycleOrchestrator  _lifecycle;
    private readonly IPaymentProviderFactory             _providerFactory;
    private readonly ISubscriberBillingRepository        _billing;
    private readonly IBillingInvoiceService              _invoiceService;
    private readonly SubscriptionMetrics                 _metrics;
    private readonly IConfiguration                      _config;
    private readonly ILogger<SubscriptionRenewalService> _log;

    public SubscriptionRenewalService(
        ErpDbContext db,
        ISubscriptionLifecycleOrchestrator lifecycle,
        IPaymentProviderFactory providerFactory,
        ISubscriberBillingRepository billing,
        IBillingInvoiceService invoiceService,
        SubscriptionMetrics metrics,
        IConfiguration config,
        ILogger<SubscriptionRenewalService> log)
    {
        _db              = db;
        _lifecycle       = lifecycle;
        _providerFactory = providerFactory;
        _billing         = billing;
        _invoiceService  = invoiceService;
        _metrics         = metrics;
        _config          = config;
        _log             = log;
    }

    public async Task ProcessUpcomingRenewalsAsync(int lookAheadHours = 24, CancellationToken ct = default)
    {
        var now     = DateTime.UtcNow;
        var horizon = now.AddHours(Math.Max(1, lookAheadHours));

        var accounts = await _db.SubscriberBillingAccounts
            .AsNoTracking()
            .Where(a =>
                (a.Status == BillingAccountStatus.Active || a.Status == BillingAccountStatus.Trialing) &&
                a.CurrentPeriodEndUtc.HasValue &&
                a.CurrentPeriodEndUtc.Value >= now &&
                a.CurrentPeriodEndUtc.Value <= horizon &&
                a.RenewalState == BillingRenewalState.Active)
            .Select(a => new { a.SubscriberId })
            .ToListAsync(ct);

        _log.LogInformation(
            "SubscriptionRenewalService: {Count} accounts due for renewal in {Hours}h window",
            accounts.Count, lookAheadHours);

        var failures = 0;
        foreach (var account in accounts)
        {
            try   { await ProcessSubscriberRenewalAsync(account.SubscriberId, ct); }
            catch (Exception ex)
            {
                failures++;
                _log.LogError(ex,
                    "SubscriptionRenewalService: renewal failed for subscriber={SubscriberId}",
                    account.SubscriberId);
            }
        }

        if (failures > 0)
            _log.LogWarning("SubscriptionRenewalService: {F}/{T} renewals failed", failures, accounts.Count);
    }

    public async Task ProcessSubscriberRenewalAsync(Guid subscriberId, CancellationToken ct = default)
    {
        // Advisory lock prevents two Hangfire workers from renewing the same subscriber simultaneously.
        var lockKey = (long)(uint)subscriberId.GetHashCode();
        if (!await TryAcquireAdvisoryLockAsync(lockKey, ct))
        {
            _log.LogInformation(
                "SubscriptionRenewalService: subscriber={SubscriberId} locked by another worker — skipping",
                subscriberId);
            return;
        }

        var sw = Stopwatch.StartNew();
        try   { await ProcessRenewalCoreAsync(subscriberId, ct); }
        finally
        {
            await ReleaseAdvisoryLockAsync(lockKey, ct);
            sw.Stop();
            _metrics.RenewalProcessingDurationMs.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task ProcessRenewalCoreAsync(Guid subscriberId, CancellationToken ct)
    {
        var account = await _billing.GetAccountTrackedBySubscriberIdAsync(subscriberId, ct);
        if (account is null)
        {
            _log.LogWarning(
                "SubscriptionRenewalService: no billing account for subscriber={SubscriberId}", subscriberId);
            return;
        }

        // Idempotency: if period already advanced past now → skip
        if (account.CurrentPeriodEndUtc.HasValue &&
            account.CurrentPeriodEndUtc.Value > DateTime.UtcNow.AddMinutes(5))
        {
            _log.LogDebug(
                "SubscriptionRenewalService: subscriber={SubscriberId} period not yet due — skipping",
                subscriberId);
            return;
        }

        // Resolve real amount from plan
        var (planCode, planAmount) = await ResolvePlanAsync(subscriberId, ct);
        var provider    = _providerFactory.GetForSubscriber(subscriberId);
        var now         = DateTime.UtcNow;
        var periodStart = account.CurrentPeriodEndUtc ?? now;
        var periodEnd   = periodStart.AddMonths(1);
        var currency    = account.CurrencyCode;
        var sysId       = SubscriptionLifecycleOrchestrator.SystemActorId;

        _log.LogInformation(
            "SubscriptionRenewalService: subscriber={SubscriberId} plan={Plan} amount={Amount} {Currency}",
            subscriberId, planCode, planAmount, currency);

        // Create real invoice
        var invoiceId = await _invoiceService.EnsureRenewalInvoiceAsync(
            subscriberId, planCode, planAmount, currency, periodStart, periodEnd, sysId, ct);

        if (provider.ProviderType == PaymentProviderType.None)
        {
            // Manual billing — advance period without actual charge
            await AdvancePeriodAsync(account, periodEnd, sysId, ct);
            _metrics.RenewalSuccessTotal.Add(1);
            return;
        }

        // Record attempt with real amount
        var attemptId = await _invoiceService.RecordPaymentAttemptAsync(
            invoiceId, subscriberId, provider.ProviderType, planAmount, currency, sysId, ct);

        // Resolve real external customer ID
        var providerCustomer = await _billing.GetProviderCustomerAsync(subscriberId, provider.ProviderType, ct);
        var externalCustomerId = providerCustomer?.ExternalCustomerId ?? string.Empty;

        var chargeResult = await provider.ChargeInvoiceAsync(
            new ChargeInvoiceRequest(
                subscriberId,
                ExternalCustomerId: externalCustomerId,
                ExternalInvoiceId:  null,
                Amount:             planAmount,
                CurrencyCode:       currency,
                Description:        $"Renewal {planCode} — {periodStart:yyyy-MM}"),
            ct);

        if (chargeResult.Succeeded && chargeResult.Data?.Succeeded == true)
        {
            await _invoiceService.CompletePaymentAsync(
                attemptId, invoiceId, subscriberId,
                chargeResult.Data.ProviderReference, sysId, ct);

            await AdvancePeriodAsync(account, periodEnd, sysId, ct);
            _metrics.RenewalSuccessTotal.Add(1);
        }
        else
        {
            var reason = chargeResult.Data?.FailureReason ?? chargeResult.ErrorMessage ?? "Payment failed";
            await _invoiceService.FailPaymentAsync(attemptId, subscriberId, reason, chargeResult.Data?.FailureCode, sysId, ct);

            _log.LogWarning(
                "SubscriptionRenewalService: payment failed subscriber={SubscriberId} reason={Reason}",
                subscriberId, reason);

            var graceDays = _config.GetValue("SaaS:RenewalGracePeriodDays", 7);
            await _lifecycle.EnterGracePeriodAsync(
                subscriberId, DateTime.UtcNow.AddDays(graceDays), sysId, ct);

            _metrics.RenewalFailureTotal.Add(1);
        }
    }

    private async Task AdvancePeriodAsync(
        ERP.Domain.Billing.Entities.SubscriberBillingAccount account,
        DateTime newEnd, Guid actorId, CancellationToken ct)
    {
        account.SetCurrentPeriodEnd(newEnd, actorId);
        account.Activate(actorId);
        await _billing.SaveChangesAsync(ct);
    }

    private async Task<(string planCode, decimal amount)> ResolvePlanAsync(Guid subscriberId, CancellationToken ct)
    {
        var subscription = await _db.Set<SubscriberSubscription>()
            .AsNoTracking()
            .Where(s => s.SubscriberId == subscriberId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial))
            .FirstOrDefaultAsync(ct);

        if (subscription is null) return ("starter", 49.00m);

        var plan = await _db.CommercialPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == subscription.PlanId, ct);

        return plan is null ? ("starter", 49.00m) : (plan.Code, plan.PriceAmount);
    }

    // ── PostgreSQL advisory lock ───────────────────────────────────────────────

    private async Task<bool> TryAcquireAdvisoryLockAsync(long lockKey, CancellationToken ct)
    {
        try
        {
            var rows = await _db.Database
                .SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({lockKey})")
                .ToListAsync(ct);
            return rows.FirstOrDefault(true);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "SubscriptionRenewalService: advisory lock unavailable — proceeding without lock");
            return true; // fail-open
        }
    }

    private async Task ReleaseAdvisoryLockAsync(long lockKey, CancellationToken ct)
    {
        try
        {
            await _db.Database
                .SqlQueryRaw<bool>($"SELECT pg_advisory_unlock({lockKey})")
                .ToListAsync(ct);
        }
        catch { /* best-effort */ }
    }
}
