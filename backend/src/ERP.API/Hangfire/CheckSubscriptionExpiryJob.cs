using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using ERP.Infrastructure.SaaS;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Hangfire;

public interface ICheckSubscriptionExpiryJob
{
    Task ExecuteAsync(CancellationToken ct = default);
}

/// <summary>
/// Hangfire job that automatically transitions expired Trial/GracePeriod subscribers.
///
/// IDEMPOTENCY: Each check guards on the current lifecycle status before transitioning.
///   - If already GracePeriod or Suspended → skip (safe to re-run).
///   - Uses ISubscriptionLifecycleOrchestrator for atomic, consistent transitions.
///
/// ACTOR: Uses SubscriptionLifecycleOrchestrator.SystemActorId (00000000-…-0001).
/// </summary>
public sealed class CheckSubscriptionExpiryJob : ICheckSubscriptionExpiryJob
{
    private readonly ISubscriberRepository                  _subscribers;
    private readonly ISubscriberBillingRepository           _billing;
    private readonly ILogger<CheckSubscriptionExpiryJob>    _logger;
    private readonly IConfiguration                         _config;
    private readonly IServiceScopeFactory                   _scopeFactory;

    public CheckSubscriptionExpiryJob(
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing,
        ILogger<CheckSubscriptionExpiryJob> logger,
        IConfiguration config,
        IServiceScopeFactory scopeFactory)
    {
        _subscribers  = subscribers;
        _billing      = billing;
        _logger       = logger;
        _config       = config;
        _scopeFactory = scopeFactory;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now       = DateTime.UtcNow;
        var graceDays = _config.GetValue("SaaS:TrialGracePeriodDays", 7);
        var all       = await _subscribers.GetAllAsync(ct);

        var failures  = 0;
        foreach (var subscriber in all)
        {
            try
            {
                await ProcessSubscriberAsync(subscriber, now, graceDays, ct);
            }
            catch (Exception ex)
            {
                failures++;
                _logger.LogError(ex,
                    "CheckSubscriptionExpiryJob: error procesando subscriber={SubscriberId}",
                    subscriber.Id);
            }
        }

        if (failures > 0)
            _logger.LogWarning(
                "CheckSubscriptionExpiryJob: {Failures}/{Total} subscribers fallaron al procesar.",
                failures, all.Count);
    }

    private async Task ProcessSubscriberAsync(
        Subscriber subscriber,
        DateTime now,
        int graceDays,
        CancellationToken ct)
    {
        // Use a fresh scope per subscriber so the orchestrator gets a clean DbContext
        await using var scope = _scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycleOrchestrator>();

        var account = await _billing.GetAccountBySubscriberIdAsync(subscriber.Id, ct);

        // ── IDEMPOTENCY GUARD ────────────────────────────────────────────────────
        // If BillingAccount is missing: log and skip (do NOT throw — just warn)
        if (account is null)
        {
            if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Trial)
                _logger.LogWarning(
                    "CheckSubscriptionExpiryJob: subscriber={SubscriberId} está en Trial pero no tiene BillingAccount.",
                    subscriber.Id);
            return;
        }

        // ── Trial vencido → GracePeriod ──────────────────────────────────────────
        // Guard: ONLY transition if STILL in Trial (prevents double execution)
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Trial
            && account.Status           == BillingAccountStatus.Trialing
            && account.TrialEndsAtUtc.HasValue
            && account.TrialEndsAtUtc.Value < now)
        {
            var graceEnd = now.AddDays(graceDays);
            _logger.LogInformation(
                "CheckSubscriptionExpiryJob: subscriber={SubscriberId} trial vencido → GracePeriod hasta {GraceEnd}",
                subscriber.Id, graceEnd);

            await orchestrator.EnterGracePeriodAsync(
                subscriber.Id,
                graceEnd,
                SubscriptionLifecycleOrchestrator.SystemActorId,
                ct);
            return;
        }

        // ── GracePeriod vencido → Suspended ──────────────────────────────────────
        // Guard: ONLY transition if STILL in GracePeriod (prevents double execution)
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod
            && account.Status           == BillingAccountStatus.GracePeriod
            && account.GracePeriodEndsAtUtc.HasValue
            && account.GracePeriodEndsAtUtc.Value < now)
        {
            _logger.LogWarning(
                "CheckSubscriptionExpiryJob: subscriber={SubscriberId} período de gracia vencido → Suspended",
                subscriber.Id);

            await orchestrator.SuspendAsync(
                subscriber.Id,
                "Período de gracia vencido — suspensión automática.",
                SubscriptionLifecycleOrchestrator.SystemActorId,
                ct);
        }
    }
}
