using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Billing.Governance;

public sealed class BillingGovernanceService : IBillingGovernanceService
{
    private readonly ISubscriberBillingRepository _billing;
    private readonly ISubscriberRepository _subscribers;
    private readonly ISubscriberEntitlementsCacheInvalidator _entitlementsCache;

    public BillingGovernanceService(
        ISubscriberBillingRepository billing,
        ISubscriberRepository subscribers,
        ISubscriberEntitlementsCacheInvalidator entitlementsCache)
    {
        _billing = billing;
        _subscribers = subscribers;
        _entitlementsCache = entitlementsCache;
    }

    public async Task<BillingGovernanceState> GetStateAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var account = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        var utcNow = DateTime.UtcNow;
        if (account is null)
        {
            return new BillingGovernanceState(
                subscriberId,
                null,
                BillingRenewalState.Active,
                BillingTrialState.None,
                AllowsPlatformAccess: true,
                null,
                null);
        }

        return new BillingGovernanceState(
            subscriberId,
            account.Status,
            account.RenewalState,
            account.TrialState,
            account.AllowsPlatformAccess(utcNow),
            account.GracePeriodEndsAtUtc,
            account.CurrentPeriodEndUtc);
    }

    public async Task<bool> CanAccessPlatformAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return true;

        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null || !subscriber.IsActive)
            return false;

        var state = await GetStateAsync(subscriberId, ct);
        return state.AllowsPlatformAccess;
    }

    public async Task EnsureBillingAccountAsync(
        Guid subscriberId,
        string billingEmail,
        Guid actorId,
        CancellationToken ct = default)
    {
        var existing = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        if (existing is not null)
            return;

        var account = SubscriberBillingAccount.Create(subscriberId, billingEmail, actorId);
        await _billing.AddAccountAsync(account, ct);
        await _billing.AddBillingEventAsync(
            BillingEvent.Record(
                subscriberId,
                BillingEvent.Types.AccountCreated,
                BillingEventSource.System,
                actorId),
            ct);
        await _billing.SaveChangesAsync(ct);
    }

    public async Task StartGracePeriodAsync(
        Guid subscriberId,
        int graceDays,
        Guid actorId,
        CancellationToken ct = default)
    {
        var account = await RequireAccountTrackedAsync(subscriberId, ct);
        var ends = DateTime.UtcNow.AddDays(Math.Max(1, graceDays));
        account.EnterGracePeriod(ends, actorId);
        await RecordAndSaveAsync(
            subscriberId,
            BillingEvent.Types.GracePeriodStarted,
            BillingEventSource.System,
            actorId,
            $"{{\"graceEndsAtUtc\":\"{ends:O}\"}}",
            ct);
        await _entitlementsCache.InvalidateAsync(subscriberId, ct);
    }

    public async Task SuspendForNonPaymentAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default)
    {
        var account = await RequireAccountTrackedAsync(subscriberId, ct);
        account.Suspend(actorId);
        await RecordAndSaveAsync(subscriberId, BillingEvent.Types.Suspended, BillingEventSource.System, actorId, null, ct);
        await _entitlementsCache.InvalidateAsync(subscriberId, ct);
    }

    public async Task ReactivateAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default)
    {
        var account = await RequireAccountTrackedAsync(subscriberId, ct);
        account.Activate(actorId);
        await RecordAndSaveAsync(subscriberId, BillingEvent.Types.Reactivated, BillingEventSource.System, actorId, null, ct);
        await _entitlementsCache.InvalidateAsync(subscriberId, ct);
    }

    public async Task RecordEventAsync(
        Guid subscriberId,
        string eventType,
        BillingEventSource source,
        Guid actorId,
        string? payloadJson = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        await _billing.AddBillingEventAsync(
            BillingEvent.Record(subscriberId, eventType, source, actorId, payloadJson, correlationId),
            ct);
        await _billing.SaveChangesAsync(ct);
    }

    private async Task<SubscriberBillingAccount> RequireAccountTrackedAsync(Guid subscriberId, CancellationToken ct)
    {
        var account = await _billing.GetAccountTrackedBySubscriberIdAsync(subscriberId, ct);
        if (account is null)
            throw new InvalidOperationException("Billing account not found for subscriber.");
        return account;
    }

    private async Task RecordAndSaveAsync(
        Guid subscriberId,
        string eventType,
        BillingEventSource source,
        Guid actorId,
        string? payloadJson,
        CancellationToken ct)
    {
        await _billing.AddBillingEventAsync(
            BillingEvent.Record(subscriberId, eventType, source, actorId, payloadJson),
            ct);
        await _billing.SaveChangesAsync(ct);
    }
}
