using System.Data;
using ERP.Application.Common.Subscriptions;
using Microsoft.EntityFrameworkCore.Storage;
using ERP.Application.Platform.Audit;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Platform.Audit.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Events;
using ERP.Domain.Subscribers.Exceptions;
using ERP.Domain.Subscribers.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Atomic lifecycle transitions: Subscriber + BillingAccount change inside a single
/// serializable transaction. Every method validates the current state before mutating.
/// </summary>
public sealed class SubscriptionLifecycleOrchestrator : ISubscriptionLifecycleOrchestrator
{
    // Pseudo-ID for system-initiated transitions (jobs, automation).
    public static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000001");

    private readonly ErpDbContext                        _db;
    private readonly IPlatformAuditLogger                _audit;
    private readonly ISubscriberEntitlementsCacheInvalidator _cache;

    public SubscriptionLifecycleOrchestrator(
        ErpDbContext db,
        IPlatformAuditLogger audit,
        ISubscriberEntitlementsCacheInvalidator cache)
    {
        _db    = db;
        _audit = audit;
        _cache = cache;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public transitions
    // ──────────────────────────────────────────────────────────────────────────

    public async Task ActivateAsync(
        Guid subscriberId, Guid actorId, string? notes = null, CancellationToken ct = default)
    {
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothAsync(subscriberId, ct);

        // Inactive is a terminal state — cannot be reactivated through normal flow
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Inactive)
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Active");

        var previousStatus = subscriber.LifecycleStatus.ToString();

        subscriber.Activate(actorId);
        account.Activate(actorId);

        subscriber.AddDomainEvent(new SubscriptionActivatedEvent(subscriberId, actorId, notes));

        await SaveAndInvalidateAsync(subscriberId, ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.SubscriberActivated,
            actorId == SystemActorId ? null : actorId,
            actorId == SystemActorId ? "system" : null,
            subscriberId,
            resourceType: "Subscriber",
            resourceId: subscriberId,
            oldValueJson: $"{{\"status\":\"{previousStatus}\"}}",
            newValueJson: "{\"status\":\"Active\"}",
            notes: notes,
            ct: ct);
    }

    public async Task SuspendAsync(
        Guid subscriberId, string reason, Guid actorId, CancellationToken ct = default)
    {
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothAsync(subscriberId, ct);

        // Cancelled is terminal — suspending is a no-op guard
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.Inactive)
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Suspended");

        if (account.Status == BillingAccountStatus.Cancelled)
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Suspended (account cancelled)");

        var previousStatus = subscriber.LifecycleStatus.ToString();

        subscriber.Suspend(reason, actorId);
        account.Suspend(actorId);

        subscriber.AddDomainEvent(new SubscriptionSuspendedEvent(subscriberId, actorId, reason));

        await SaveAndInvalidateAsync(subscriberId, ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.SubscriberSuspended,
            actorId == SystemActorId ? null : actorId,
            actorId == SystemActorId ? "system" : null,
            subscriberId,
            resourceType: "Subscriber",
            resourceId: subscriberId,
            oldValueJson: $"{{\"status\":\"{previousStatus}\"}}",
            newValueJson: $"{{\"status\":\"Suspended\",\"reason\":\"{reason}\"}}",
            notes: reason,
            ct: ct);
    }

    public async Task StartTrialAsync(
        Guid subscriberId, DateTime endsAtUtc, Guid actorId, CancellationToken ct = default)
    {
        if (endsAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("endsAtUtc debe ser una fecha futura.", nameof(endsAtUtc));

        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothAsync(subscriberId, ct);

        // Trial only from Active or existing Trial (re-extension)
        if (subscriber.LifecycleStatus is not (
            SubscriberLifecycleStatus.Active or SubscriberLifecycleStatus.Trial))
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "Trial");

        subscriber.SetTrial(actorId);
        account.SetTrial(BillingTrialState.Active, endsAtUtc, actorId);

        subscriber.AddDomainEvent(new TrialStartedEvent(subscriberId, actorId, endsAtUtc));

        await SaveAndInvalidateAsync(subscriberId, ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.TrialStarted,
            actorId == SystemActorId ? null : actorId,
            actorId == SystemActorId ? "system" : null,
            subscriberId,
            resourceType: "Subscriber",
            resourceId: subscriberId,
            newValueJson: $"{{\"trialEndsAtUtc\":\"{endsAtUtc:O}\"}}",
            ct: ct);
    }

    public async Task EnterGracePeriodAsync(
        Guid subscriberId, DateTime endsAtUtc, Guid actorId, CancellationToken ct = default)
    {
        if (endsAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("endsAtUtc debe ser una fecha futura.", nameof(endsAtUtc));

        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothAsync(subscriberId, ct);

        // Guard: only enter GracePeriod from Trial or Active
        if (subscriber.LifecycleStatus is not (
            SubscriberLifecycleStatus.Trial or SubscriberLifecycleStatus.Active))
            throw new InvalidLifecycleTransitionException(subscriber.LifecycleStatus, "GracePeriod");

        // Idempotency: already in GracePeriod → only update date if earlier
        if (subscriber.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod &&
            account.GracePeriodEndsAtUtc.HasValue &&
            account.GracePeriodEndsAtUtc.Value >= endsAtUtc)
            return;

        subscriber.EnterGracePeriod(actorId);
        account.EnterGracePeriod(endsAtUtc, actorId);

        subscriber.AddDomainEvent(new GracePeriodStartedEvent(subscriberId, actorId, endsAtUtc));

        await SaveAndInvalidateAsync(subscriberId, ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.GracePeriodStarted,
            actorId == SystemActorId ? null : actorId,
            actorId == SystemActorId ? "system" : null,
            subscriberId,
            resourceType: "Subscriber",
            resourceId: subscriberId,
            newValueJson: $"{{\"gracePeriodEndsAtUtc\":\"{endsAtUtc:O}\"}}",
            ct: ct);
    }

    public async Task CancelAsync(
        Guid subscriberId, string reason, Guid actorId, CancellationToken ct = default)
    {
        await using var tx = await BeginTxAsync(ct);

        var (subscriber, account) = await LoadBothAsync(subscriberId, ct);

        if (account.Status == BillingAccountStatus.Cancelled)
            return; // already cancelled — idempotent

        subscriber.Suspend(reason, actorId);
        account.Cancel(BillingRenewalState.Cancelled, actorId);

        subscriber.AddDomainEvent(new SubscriptionCancelledEvent(subscriberId, actorId, reason));

        await SaveAndInvalidateAsync(subscriberId, ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync(
            PlatformAuditLog.Actions.SubscriberSuspended,
            actorId == SystemActorId ? null : actorId,
            actorId == SystemActorId ? "system" : null,
            subscriberId,
            resourceType: "Subscriber",
            resourceId: subscriberId,
            newValueJson: $"{{\"status\":\"Cancelled\",\"reason\":\"{reason}\"}}",
            notes: reason,
            ct: ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<IDbContextTransaction> BeginTxAsync(CancellationToken ct)
        => await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

    private async Task<(Subscriber subscriber, SubscriberBillingAccount account)> LoadBothAsync(
        Guid subscriberId, CancellationToken ct)
    {
        // Load with tracking so EF can persist changes in one SaveChanges call
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

    private async Task SaveAndInvalidateAsync(Guid subscriberId, CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateAsync(subscriberId, ct);
    }
}
