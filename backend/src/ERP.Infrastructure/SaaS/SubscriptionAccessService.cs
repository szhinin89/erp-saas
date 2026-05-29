using System.Diagnostics;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Cache-first subscription access evaluator.
///
/// HOT PATH (cache hit):    O(1), ~0-1ms — no DB query.
/// COLD PATH (cache miss):  1 JOIN query, ~5-20ms — result cached immediately.
///
/// Uses a compiled EF query (FASE 6) for the snapshot rebuild to avoid
/// repeated LINQ expression tree compilation.
/// </summary>
public sealed class SubscriptionAccessService : ISubscriptionAccessService
{
    // ── FASE 6: Compiled query — avoids LINQ re-compilation on every cache miss ─
    // Projects directly into snapshot DTO — no entity tracking, no navigation loads.
    private static readonly Func<ErpDbContext, Guid, CancellationToken, Task<SnapshotProjection?>>
        GetSnapshotQuery = EF.CompileAsyncQuery(
            (ErpDbContext db, Guid subscriberId, CancellationToken _) =>
                db.Subscribers
                  .AsNoTracking()
                  .Where(s => s.Id == subscriberId)
                  .Join(
                      db.SubscriberBillingAccounts.AsNoTracking(),
                      s => s.Id,
                      a => a.SubscriberId,
                      (s, a) => new SnapshotProjection
                      {
                          LifecycleStatus      = s.LifecycleStatus,
                          SuspendedReason      = s.SuspendedReason,
                          PlanCode             = s.PlanCode,
                          AccountStatus        = a.Status,
                          TrialEndsAtUtc       = a.TrialEndsAtUtc,
                          GracePeriodEndsAtUtc = a.GracePeriodEndsAtUtc,
                      })
                  .FirstOrDefault());

    // Fallback: subscriber only (when no billing account yet — pre-provisioning)
    private static readonly Func<ErpDbContext, Guid, CancellationToken, Task<SubscriberProjection?>>
        GetSubscriberQuery = EF.CompileAsyncQuery(
            (ErpDbContext db, Guid subscriberId, CancellationToken _) =>
                db.Subscribers
                  .AsNoTracking()
                  .Where(s => s.Id == subscriberId)
                  .Select(s => new SubscriberProjection
                  {
                      LifecycleStatus = s.LifecycleStatus,
                      SuspendedReason = s.SuspendedReason,
                      PlanCode        = s.PlanCode,
                  })
                  .FirstOrDefault());

    private readonly ErpDbContext              _db;
    private readonly ISubscriptionAccessCache  _cache;
    private readonly SubscriptionMetrics       _metrics;
    private readonly ILogger<SubscriptionAccessService> _log;

    public SubscriptionAccessService(
        ErpDbContext db,
        ISubscriptionAccessCache cache,
        SubscriptionMetrics metrics,
        ILogger<SubscriptionAccessService> log)
    {
        _db      = db;
        _cache   = cache;
        _metrics = metrics;
        _log     = log;
    }

    public async Task<SubscriptionAccessResult> EvaluateAsync(
        Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return SubscriptionAccessResult.Allow();

        // ── CACHE HIT (FASE 4) ────────────────────────────────────────────────
        var cached = await _cache.TryGetAsync(subscriberId, ct);
        if (cached is not null)
        {
            _metrics.AccessCacheHits.Add(1);
            var cachedResult = cached.ToResult();

            _log.LogDebug(
                "subscription-access: cache_hit subscriber={SubscriberId} mode={Mode} reason={Reason}",
                subscriberId, cached.AccessMode, cached.DenialReason);

            RecordAccessMetric(cachedResult);
            return cachedResult;
        }

        _metrics.AccessCacheMisses.Add(1);

        // ── CACHE MISS: rebuild from DB ───────────────────────────────────────
        var sw       = Stopwatch.StartNew();
        var snapshot = await BuildSnapshotAsync(subscriberId, ct);
        sw.Stop();

        _metrics.SnapshotRebuildDurationMs.Record(sw.Elapsed.TotalMilliseconds);

        await _cache.SetAsync(snapshot, ct);

        var result = snapshot.ToResult();

        _log.LogDebug(
            "subscription-access: db_rebuild subscriber={SubscriberId} mode={Mode} reason={Reason} duration_ms={Ms}",
            subscriberId, snapshot.AccessMode, snapshot.DenialReason, sw.Elapsed.TotalMilliseconds);

        RecordAccessMetric(result);
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<SubscriptionAccessSnapshot> BuildSnapshotAsync(
        Guid subscriberId, CancellationToken ct)
    {
        var utcNow  = DateTime.UtcNow;
        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Try JOIN first (subscriber + billing account in one query)
        var joined = await GetSnapshotQuery(_db, subscriberId, ct);

        if (joined is null)
        {
            // Check if subscriber exists at all
            var sub = await GetSubscriberQuery(_db, subscriberId, ct);
            if (sub is null)
            {
                return Blocked(subscriberId, version, SubscriptionAccessDenialReason.Inactive,
                    "subscription_inactive", null, utcNow);
            }

            // Subscriber exists but no billing account — pre-provisioning
            return sub.LifecycleStatus switch
            {
                SubscriberLifecycleStatus.Suspended => Blocked(subscriberId, version,
                    SubscriptionAccessDenialReason.Suspended, "subscription_suspended", sub.PlanCode, utcNow),
                SubscriberLifecycleStatus.Inactive  => Blocked(subscriberId, version,
                    SubscriptionAccessDenialReason.Inactive, "subscription_inactive", sub.PlanCode, utcNow),
                _                                   => Full(subscriberId, version, sub.PlanCode, utcNow),
            };
        }

        // Subscriber-level hard blocks
        if (joined.LifecycleStatus == SubscriberLifecycleStatus.Suspended)
            return Blocked(subscriberId, version, SubscriptionAccessDenialReason.Suspended,
                "subscription_suspended", joined.PlanCode, utcNow);

        if (joined.LifecycleStatus == SubscriberLifecycleStatus.Inactive)
            return Blocked(subscriberId, version, SubscriptionAccessDenialReason.Inactive,
                "subscription_inactive", joined.PlanCode, utcNow);

        // Billing-level decisions
        return joined.AccountStatus switch
        {
            BillingAccountStatus.Cancelled =>
                Blocked(subscriberId, version, SubscriptionAccessDenialReason.Cancelled,
                    "subscription_cancelled", joined.PlanCode, utcNow),

            BillingAccountStatus.Suspended =>
                Blocked(subscriberId, version, SubscriptionAccessDenialReason.Suspended,
                    "subscription_suspended", joined.PlanCode, utcNow),

            BillingAccountStatus.GracePeriod =>
                joined.GracePeriodEndsAtUtc.HasValue && joined.GracePeriodEndsAtUtc.Value < utcNow
                    ? Blocked(subscriberId, version, SubscriptionAccessDenialReason.GracePeriodExpired,
                        "subscription_grace_period_expired", joined.PlanCode, utcNow,
                        gracePeriodEndsAtUtc: joined.GracePeriodEndsAtUtc)
                    : ReadOnly(subscriberId, version, SubscriptionAccessDenialReason.GracePeriodExpired,
                        "subscription_grace_period_expired", joined.PlanCode, utcNow,
                        gracePeriodEndsAtUtc: joined.GracePeriodEndsAtUtc),

            BillingAccountStatus.Trialing =>
                joined.TrialEndsAtUtc.HasValue && joined.TrialEndsAtUtc.Value < utcNow
                    ? Blocked(subscriberId, version, SubscriptionAccessDenialReason.TrialExpired,
                        "subscription_trial_expired", joined.PlanCode, utcNow,
                        trialEndsAtUtc: joined.TrialEndsAtUtc)
                    : Full(subscriberId, version, joined.PlanCode, utcNow,
                        trialEndsAtUtc: joined.TrialEndsAtUtc),

            BillingAccountStatus.PastDue =>
                ReadOnly(subscriberId, version, SubscriptionAccessDenialReason.BillingPastDue,
                    "subscription_past_due", joined.PlanCode, utcNow),

            _ => Full(subscriberId, version, joined.PlanCode, utcNow),
        };
    }

    private void RecordAccessMetric(SubscriptionAccessResult r)
    {
        if (r.AccessMode == SubscriptionAccessMode.Blocked)
            _metrics.AccessDeniedTotal.Add(1, new KeyValuePair<string, object?>("reason", r.DenialReason.ToString()));
        else
            _metrics.AccessAllowedTotal.Add(1);
    }

    // ── Snapshot factory helpers ──────────────────────────────────────────────

    private static SubscriptionAccessSnapshot Full(
        Guid id, long version, string? planCode, DateTime eval,
        DateTime? trialEndsAtUtc = null, DateTime? gracePeriodEndsAtUtc = null) =>
        new()
        {
            SubscriberId         = id,
            AccessMode           = SubscriptionAccessMode.FullAccess,
            DenialReason         = SubscriptionAccessDenialReason.None,
            PlanCode             = planCode,
            TrialEndsAtUtc       = trialEndsAtUtc,
            GracePeriodEndsAtUtc = gracePeriodEndsAtUtc,
            Version              = version,
            EvaluatedAtUtc       = eval,
        };

    private static SubscriptionAccessSnapshot ReadOnly(
        Guid id, long version, SubscriptionAccessDenialReason reason, string code,
        string? planCode, DateTime eval,
        DateTime? trialEndsAtUtc = null, DateTime? gracePeriodEndsAtUtc = null) =>
        new()
        {
            SubscriberId         = id,
            AccessMode           = SubscriptionAccessMode.ReadOnly,
            DenialReason         = reason,
            DenialCode           = code,
            PlanCode             = planCode,
            TrialEndsAtUtc       = trialEndsAtUtc,
            GracePeriodEndsAtUtc = gracePeriodEndsAtUtc,
            Version              = version,
            EvaluatedAtUtc       = eval,
        };

    private static SubscriptionAccessSnapshot Blocked(
        Guid id, long version, SubscriptionAccessDenialReason reason, string code,
        string? planCode, DateTime eval,
        DateTime? trialEndsAtUtc = null, DateTime? gracePeriodEndsAtUtc = null) =>
        new()
        {
            SubscriberId         = id,
            AccessMode           = SubscriptionAccessMode.Blocked,
            DenialReason         = reason,
            DenialCode           = code,
            PlanCode             = planCode,
            TrialEndsAtUtc       = trialEndsAtUtc,
            GracePeriodEndsAtUtc = gracePeriodEndsAtUtc,
            Version              = version,
            EvaluatedAtUtc       = eval,
        };

    // ── Projection DTOs (lightweight, no EF tracking) ────────────────────────
    private sealed class SnapshotProjection
    {
        public SubscriberLifecycleStatus LifecycleStatus      { get; init; }
        public string?                   SuspendedReason      { get; init; }
        public string?                   PlanCode             { get; init; }
        public BillingAccountStatus      AccountStatus        { get; init; }
        public DateTime?                 TrialEndsAtUtc       { get; init; }
        public DateTime?                 GracePeriodEndsAtUtc { get; init; }
    }

    private sealed class SubscriberProjection
    {
        public SubscriberLifecycleStatus LifecycleStatus { get; init; }
        public string?                   SuspendedReason { get; init; }
        public string?                   PlanCode        { get; init; }
    }
}
