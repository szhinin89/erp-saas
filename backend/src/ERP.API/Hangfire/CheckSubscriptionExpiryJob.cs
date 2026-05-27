using ERP.Application.Common;
using ERP.Application.Platform.Audit;
using ERP.Application.Platform.Subscribers.UseCases;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Platform.Audit.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.API.Hangfire;

public interface ICheckSubscriptionExpiryJob
{
    Task ExecuteAsync(CancellationToken ct = default);
}

/// <summary>
/// Hangfire job que verifica suscriptores vencidos y los mueve al estado correcto:
/// - Trial vencido → GracePeriod (configurable, default 7 días) o Suspended.
/// - GracePeriod vencido → Suspended.
/// Registrar en program con RecurringJob.AddOrUpdate("check-subscription-expiry", ...).
/// </summary>
public sealed class CheckSubscriptionExpiryJob : ICheckSubscriptionExpiryJob
{
    private readonly ISubscriberRepository _subscribers;
    private readonly ISubscriberBillingRepository _billing;
    private readonly IMediator _mediator;
    private readonly IPlatformAuditLogger _audit;
    private readonly ISubscriberEntitlementsCacheInvalidator _cacheInvalidator;
    private readonly ILogger<CheckSubscriptionExpiryJob> _logger;
    private readonly IConfiguration _config;

    public CheckSubscriptionExpiryJob(
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing,
        IMediator mediator,
        IPlatformAuditLogger audit,
        ISubscriberEntitlementsCacheInvalidator cacheInvalidator,
        ILogger<CheckSubscriptionExpiryJob> logger,
        IConfiguration config)
    {
        _subscribers = subscribers;
        _billing = billing;
        _mediator = mediator;
        _audit = audit;
        _cacheInvalidator = cacheInvalidator;
        _logger = logger;
        _config = config;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var graceDays = _config.GetValue("SaaS:TrialGracePeriodDays", 7);
        var all = await _subscribers.GetAllAsync(ct);

        foreach (var subscriber in all)
        {
            try
            {
                await ProcessSubscriberAsync(subscriber, now, graceDays, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CheckSubscriptionExpiryJob: error procesando subscriber={SubscriberId}", subscriber.Id);
            }
        }
    }

    private async Task ProcessSubscriberAsync(
        Subscriber subscriber,
        DateTime now,
        int graceDays,
        CancellationToken ct)
    {
        var systemUserId = Guid.Empty;
        var account = await _billing.GetAccountTrackedBySubscriberIdAsync(subscriber.Id, ct);

        // Trial vencido → GracePeriod
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Trial
            && account?.TrialEndsAtUtc.HasValue == true
            && account.TrialEndsAtUtc!.Value < now)
        {
            var graceEnd = now.AddDays(graceDays);
            subscriber.EnterGracePeriod(systemUserId);
            account.EnterGracePeriod(graceEnd, systemUserId);
            await _subscribers.SaveChangesAsync(ct);
            await _cacheInvalidator.InvalidateAsync(subscriber.Id, ct);

            await _audit.LogAsync(
                PlatformAuditLog.Actions.GracePeriodStarted,
                actorUserId: null,
                actorEmail: "system",
                targetSubscriberId: subscriber.Id,
                resourceType: "Subscriber",
                resourceId: subscriber.Id,
                notes: $"Trial vencido ({account.TrialEndsAtUtc:O}). Período de gracia hasta {graceEnd:O}.",
                ct: ct);

            _logger.LogInformation(
                "CheckSubscriptionExpiryJob: subscriber={SubscriberId} trial vencido → GracePeriod until {GraceEnd}",
                subscriber.Id, graceEnd);
            return;
        }

        // GracePeriod vencido → Suspended
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod
            && account?.GracePeriodEndsAtUtc.HasValue == true
            && account.GracePeriodEndsAtUtc!.Value < now)
        {
            subscriber.Suspend("Período de gracia vencido — suspensión automática.", systemUserId);
            account.Suspend(systemUserId);
            await _subscribers.SaveChangesAsync(ct);
            await _cacheInvalidator.InvalidateAsync(subscriber.Id, ct);

            await _audit.LogAsync(
                PlatformAuditLog.Actions.SubscriberSuspended,
                actorUserId: null,
                actorEmail: "system",
                targetSubscriberId: subscriber.Id,
                resourceType: "Subscriber",
                resourceId: subscriber.Id,
                notes: $"Período de gracia vencido ({account.GracePeriodEndsAtUtc:O}). Suspensión automática.",
                ct: ct);

            _logger.LogWarning(
                "CheckSubscriptionExpiryJob: subscriber={SubscriberId} período de gracia vencido → Suspended",
                subscriber.Id);
        }
    }
}
