using System.Data;
using System.Diagnostics;
using ERP.Application.Access.Caching;
using ERP.Application.Common.Subscriptions;
using ERP.Application.Platform.Audit;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Platform.Audit.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Events;
using ERP.Domain.Subscribers.Exceptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Atomic lifecycle transitions: Subscriber + BillingAccount change inside a single
/// RepeatableRead transaction.
///
/// STATE MACHINE (FASE 10):
///   Active      → Trial, GracePeriod, Suspended, Cancelled
///   Trial       → GracePeriod, Suspended, Cancelled, Active (re-activate)
///   GracePeriod → Suspended, Cancelled, Active (payment received)
///   Suspended   → Active (reactivation), Cancelled
///   Inactive    → [terminal — no transitions allowed]
///   Cancelled   → [terminal — no transitions allowed, idempotent]
///
/// CACHE (FASE 4/11): Invalidates ISubscriptionAccessCache after every transition.
/// METRICS (FASE 12): Records transition duration and type counters.
/// AUDIT (FASE 13): Enriches log with Source and CorrelationId.
/// </summary>
public sealed class SubscriptionLifecycleOrchestrator : ISubscriptionLifecycleOrchestrator
{
    public static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");

    private readonly ErpDbContext                             _db;
    private readonly IPlatformAuditLogger                     _audit;
    private readonly ISubscriberEntitlementsCacheInvalidator  _entitlementsCache;
    private readonly ISubscriptionAccessCache                 _accessCache;
    private readonly IPermissionsCacheInvalidator             _permissionsCache;
    private readonly SubscriptionMetrics                      _metrics;
    private readonly SubscriptionBillingOptions               _opts;
    private readonly ILogger<SubscriptionLifecycleOrchestrator> _log;

    public SubscriptionLifecycleOrchestrator(
        ErpDbContext db,
        IPlatformAuditLogger audit,
        ISubscriberEntitlementsCacheInvalidator entitlementsCache,
        ISubscriptionAccessCache accessCache,
        IPermissionsCacheInvalidator permissionsCache,
        SubscriptionMetrics metrics,
        IOptions<SubscriptionBillingOptions> options,
        ILogger<SubscriptionLifecycleOrchestrator> log)
    {
        _db                = db;
        _audit             = audit;
        _entitlementsCache = entitlementsCache;
        _accessCache       = accessCache;
        _permissionsCache  = permissionsCache;
        _metrics           = metrics;
        _opts              = options.Value;
        _log               = log;
    }

    // ── Public transitions ────────────────────────────────────────────────────

    public async Task ActivateAsync(
        Guid subscriberId, Guid actorId, string? notes = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothTrackedAsync(subscriberId, ct);

        // FASE 10: Inactive is terminal
        AssertNotTerminal(subscriber, "Active");

        var previousStatus = subscriber.LifecycleStatus.ToString();
        subscriber.Activate(actorId);
        account.Activate(actorId);
        subscriber.AddDomainEvent(new SubscriptionActivatedEvent(subscriberId, actorId, notes));

        // FASE 6: keep SubscriberSubscription in sync
        await SyncSubscriptionStatusAsync(subscriberId, SubscriberLifecycleStatus.Active, actorId, ct);
        await SaveAndInvalidateBothCachesAsync(subscriberId, ct);
        await tx.CommitAsync(ct);
        sw.Stop();
        _metrics.LifecycleTransitionDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("transition", "Activate"));

        await AuditAsync(PlatformAuditLog.Actions.SubscriberActivated, actorId, subscriberId,
            $"{{\"status\":\"{previousStatus}\"}}", "{\"status\":\"Active\"}",
            notes: notes, source: ActorSource(actorId), ct: ct);
    }

    public async Task SuspendAsync(
        Guid subscriberId, string reason, Guid actorId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothTrackedAsync(subscriberId, ct);

        // FASE 10: Inactive and Cancelled are terminal
        AssertNotTerminal(subscriber, "Suspended");
        if (account.Status == BillingAccountStatus.Cancelled)
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Suspended (account cancelled)");

        var prev = subscriber.LifecycleStatus.ToString();
        subscriber.Suspend(reason, actorId);
        account.Suspend(actorId);
        subscriber.AddDomainEvent(new SubscriptionSuspendedEvent(subscriberId, actorId, reason));

        await SyncSubscriptionStatusAsync(subscriberId, SubscriberLifecycleStatus.Suspended, actorId, ct);
        await SaveAndInvalidateBothCachesAsync(subscriberId, ct);
        await tx.CommitAsync(ct);
        sw.Stop();
        _metrics.SubscriptionSuspendTotal.Add(1);
        _metrics.LifecycleTransitionDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("transition", "Suspend"));

        await AuditAsync(PlatformAuditLog.Actions.SubscriberSuspended, actorId, subscriberId,
            $"{{\"status\":\"{prev}\"}}", $"{{\"status\":\"Suspended\",\"reason\":\"{reason}\"}}",
            notes: reason, source: ActorSource(actorId), ct: ct);
    }

    public async Task StartTrialAsync(
        Guid subscriberId, DateTime endsAtUtc, Guid actorId, CancellationToken ct = default)
    {
        if (endsAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("endsAtUtc debe ser una fecha futura.", nameof(endsAtUtc));

        var sw = Stopwatch.StartNew();
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothTrackedAsync(subscriberId, ct);

        // FASE 10: Trial only from Active or re-extension of existing Trial
        if (subscriber.LifecycleStatus is not (SubscriberLifecycleStatus.Active or SubscriberLifecycleStatus.Trial))
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Trial");

        subscriber.SetTrial(actorId);
        account.SetTrial(BillingTrialState.Active, endsAtUtc, actorId);
        subscriber.AddDomainEvent(new TrialStartedEvent(subscriberId, actorId, endsAtUtc));

        await SyncSubscriptionStatusAsync(subscriberId, SubscriberLifecycleStatus.Trial, actorId, ct);
        await SaveAndInvalidateBothCachesAsync(subscriberId, ct);
        await tx.CommitAsync(ct);
        sw.Stop();
        _metrics.LifecycleTransitionDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("transition", "StartTrial"));

        await AuditAsync(PlatformAuditLog.Actions.TrialStarted, actorId, subscriberId,
            null, $"{{\"trialEndsAtUtc\":\"{endsAtUtc:O}\"}}",
            source: ActorSource(actorId), ct: ct);
    }

    public async Task EnterGracePeriodAsync(
        Guid subscriberId, DateTime endsAtUtc, Guid actorId, CancellationToken ct = default)
    {
        if (endsAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("endsAtUtc debe ser una fecha futura.", nameof(endsAtUtc));

        var sw = Stopwatch.StartNew();
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothTrackedAsync(subscriberId, ct);

        // FASE 10: GracePeriod only from Trial or Active
        if (subscriber.LifecycleStatus is not (SubscriberLifecycleStatus.Trial or SubscriberLifecycleStatus.Active))
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "GracePeriod");

        // Idempotency: already in grace period and the existing end is later → skip
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod &&
            account.GracePeriodEndsAtUtc.HasValue &&
            account.GracePeriodEndsAtUtc.Value >= endsAtUtc)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        subscriber.EnterGracePeriod(actorId);
        account.EnterGracePeriod(endsAtUtc, actorId);
        subscriber.AddDomainEvent(new GracePeriodStartedEvent(subscriberId, actorId, endsAtUtc));

        await SyncSubscriptionStatusAsync(subscriberId, SubscriberLifecycleStatus.GracePeriod, actorId, ct);
        await SaveAndInvalidateBothCachesAsync(subscriberId, ct);
        await tx.CommitAsync(ct);
        sw.Stop();
        _metrics.GracePeriodStartedTotal.Add(1);
        _metrics.LifecycleTransitionDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("transition", "EnterGracePeriod"));

        await AuditAsync(PlatformAuditLog.Actions.GracePeriodStarted, actorId, subscriberId,
            null, $"{{\"gracePeriodEndsAtUtc\":\"{endsAtUtc:O}\"}}",
            source: ActorSource(actorId), ct: ct);
    }

    public async Task CancelAsync(
        Guid subscriberId, string reason, Guid actorId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothTrackedAsync(subscriberId, ct);

        // Idempotent: already cancelled
        if (account.Status == BillingAccountStatus.Cancelled)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        // FASE 10: Cannot cancel Inactive
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Inactive)
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Cancelled");

        subscriber.Suspend(reason, actorId);
        account.Cancel(BillingRenewalState.Cancelled, actorId);
        subscriber.AddDomainEvent(new SubscriptionCancelledEvent(subscriberId, actorId, reason));

        await SyncSubscriptionStatusAsync(subscriberId, SubscriberLifecycleStatus.Inactive, actorId, ct);
        await SaveAndInvalidateBothCachesAsync(subscriberId, ct);
        await tx.CommitAsync(ct);
        sw.Stop();
        _metrics.LifecycleTransitionDurationMs.Record(sw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("transition", "Cancel"));

        await AuditAsync(PlatformAuditLog.Actions.SubscriberSuspended, actorId, subscriberId,
            null, $"{{\"status\":\"Cancelled\",\"reason\":\"{reason}\"}}",
            notes: reason, source: ActorSource(actorId), ct: ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void AssertNotTerminal(Subscriber subscriber, string targetState)
    {
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Inactive)
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, targetState);
    }

    private async Task<IDbContextTransaction> BeginTxAsync(CancellationToken ct)
        => await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

    private async Task<(Subscriber subscriber, SubscriberBillingAccount account)> LoadBothTrackedAsync(
        Guid subscriberId, CancellationToken ct)
    {
        var subscriber = await _db.Subscribers
            .FirstOrDefaultAsync(s => s.Id == subscriberId, ct)
            ?? throw new InvalidOperationException($"Subscriber {subscriberId} no encontrado.");

        var account = await _db.SubscriberBillingAccounts
            .FirstOrDefaultAsync(a => a.SubscriberId == subscriberId, ct)
            ?? throw new InvalidOperationException(
                $"BillingAccount faltante para subscriber {subscriberId}. " +
                "Ejecutar integridad de provisioning.");

        return (subscriber, account);
    }

    /// <summary>
    /// FASE 6: Synchronizes SubscriberSubscription.Status to match Subscriber.LifecycleStatus.
    /// Mapping: Active→Active, Trial→Trial, GracePeriod→GracePeriod, Suspended→Suspended, Cancelled→Cancelled.
    /// Called inside SaveChangesAsync so all three entities stay consistent.
    /// </summary>
    private async Task SyncSubscriptionStatusAsync(
        Guid subscriberId, SubscriberLifecycleStatus lifecycleStatus, Guid actorId, CancellationToken ct)
    {
        var subscription = await _db.Set<SubscriberSubscription>()
            .Where(s => s.SubscriberId == subscriberId &&
                        s.Status != SubscriptionStatus.Cancelled)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (subscription is null) return;

        switch (lifecycleStatus)
        {
            case SubscriberLifecycleStatus.Active:
                subscription.Reactivate(actorId);
                break;
            case SubscriberLifecycleStatus.Trial:
                subscription.StartTrial(DateTime.UtcNow.AddDays(_opts.TrialDurationDays), actorId);
                break;
            case SubscriberLifecycleStatus.GracePeriod:
                subscription.EnterGracePeriod(DateTime.UtcNow.AddDays(_opts.TrialGracePeriodDays), actorId);
                break;
            case SubscriberLifecycleStatus.Suspended:
                subscription.Suspend(actorId);
                break;
            case SubscriberLifecycleStatus.Inactive:
                subscription.Cancel(actorId);
                break;
        }
    }

    private async Task SaveAndInvalidateBothCachesAsync(Guid subscriberId, CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
        await _entitlementsCache.InvalidateAsync(subscriberId, ct);
        await _accessCache.InvalidateAsync(subscriberId, ct);
        // Also invalidate effective-permissions cache so plan changes take effect immediately.
        // Without this, users retain stale effective permissions until the cache TTL expires.
        await _permissionsCache.BumpSubscriberVersionAsync(subscriberId, ct);
    }

    // FASE 13: Audit enrichment with Source
    private async Task AuditAsync(
        string action,
        Guid actorId,
        Guid subscriberId,
        string? oldValue,
        string? newValue,
        string? notes   = null,
        string? source  = null,
        CancellationToken ct = default)
    {
        var enrichedNotes = source is null ? notes : $"[source:{source}] {notes}".Trim();

        await _audit.LogAsync(
            action,
            actorId == SystemActorId ? null : actorId,
            actorId == SystemActorId ? "system" : null,
            subscriberId,
            resourceType:  "Subscriber",
            resourceId:    subscriberId,
            oldValueJson:  oldValue,
            newValueJson:  newValue,
            notes:         enrichedNotes,
            ct:            ct);
    }

    private static string ActorSource(Guid actorId) =>
        actorId == SystemActorId ? "CheckSubscriptionExpiryJob" : "PlatformOperator";
}
