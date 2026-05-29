using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Billing.Governance;
using ERP.Application.Common.Features;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Platform;

/// <summary>
/// SaaS diagnostic panel — for PlatformOperator and SuperAdmin only.
/// Returns a complete snapshot of a subscriber's SaaS state for support/debugging.
/// Includes: lifecycle, billing, access mode, features, quotas, invoices, attempts.
/// NO sensitive data (no certificate passwords, no payment provider secrets).
/// </summary>
[ApiController]
[Route("api/platform/saas/debug")]
[Authorize(Roles = PlatformAuthorizationRoles.PlatformOperator)]
[Tags("Platform · SaaS Debug")]
public sealed class PlatformSaasDebugController : ControllerBase
{
    private readonly ISubscriberRepository        _subscribers;
    private readonly ISubscriberBillingRepository _billing;
    private readonly IBillingGovernanceService    _governance;
    private readonly ISubscriptionAccessService   _access;
    private readonly IFeatureAccessService        _features;
    private readonly ISubscriptionAccessCache     _cache;

    public PlatformSaasDebugController(
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing,
        IBillingGovernanceService governance,
        ISubscriptionAccessService access,
        IFeatureAccessService features,
        ISubscriptionAccessCache cache)
    {
        _subscribers = subscribers;
        _billing     = billing;
        _governance  = governance;
        _access      = access;
        _features    = features;
        _cache       = cache;
    }

    /// <summary>
    /// Returns a full SaaS diagnostic snapshot for a specific subscriber.
    /// Useful for support, debugging lifecycle issues, and verifying billing state.
    /// </summary>
    [HttpGet("{subscriberId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SaasDebugSnapshot>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDebugSnapshot(Guid subscriberId, CancellationToken ct)
    {
        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null)
            return this.ApiNotFound("Suscriptor no encontrado.");

        var billingAccount = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        var billingState   = await _governance.GetStateAsync(subscriberId, ct);
        var accessResult   = await _access.EvaluateAsync(subscriberId, ct);
        var featureSnapshot = await _features.GetSnapshotAsync(subscriberId, ct);
        var cachedSnapshot  = await _cache.TryGetAsync(subscriberId, ct);

        // Recent invoices (last 5)
        var recentInvoices = await _billing.GetInvoicesBySubscriberAsync(subscriberId, take: 5, ct);

        // Recent billing events (last 10)
        var recentEvents = await _billing.GetBillingEventsAsync(subscriberId, take: 10, ct);

        var snapshot = new SaasDebugSnapshot
        {
            SubscriberId       = subscriberId,
            SubscriberName     = subscriber.Name,
            SubscriberSlug     = subscriber.Slug,
            PlanCode           = subscriber.PlanCode,
            PreferredLanguage  = subscriber.PreferredLanguage,
            CheckedAtUtc       = DateTime.UtcNow,

            Lifecycle = new LifecycleDebugInfo
            {
                Status         = subscriber.LifecycleStatus.ToString(),
                IsActive       = subscriber.IsActive,
                IsOperational  = subscriber.IsOperational,
                SuspendedReason = subscriber.SuspendedReason,
                SuspendedAtUtc = subscriber.SuspendedAtUtc,
            },

            Billing = billingAccount is null ? null : new BillingDebugInfo
            {
                Status               = billingAccount.Status.ToString(),
                RenewalState         = billingAccount.RenewalState.ToString(),
                TrialState           = billingAccount.TrialState.ToString(),
                BillingEmail         = billingAccount.BillingEmail,
                CurrencyCode         = billingAccount.CurrencyCode,
                TrialEndsAtUtc       = billingAccount.TrialEndsAtUtc,
                GracePeriodEndsAtUtc = billingAccount.GracePeriodEndsAtUtc,
                CurrentPeriodEndUtc  = billingAccount.CurrentPeriodEndUtc,
                AllowsPlatformAccess = billingAccount.AllowsPlatformAccess(DateTime.UtcNow),
                PrimaryProvider      = billingAccount.PrimaryProvider.ToString(),
            },

            Access = new AccessDebugInfo
            {
                AccessMode   = accessResult.AccessMode.ToString(),
                DenialReason = accessResult.DenialReason.ToString(),
                DenialCode   = accessResult.CanAccess ? null : accessResult.DenialCode,
                CanAccess    = accessResult.CanAccess,
                IsReadOnly   = accessResult.AccessMode == SubscriptionAccessMode.ReadOnly,
                IsBlocked    = accessResult.AccessMode == SubscriptionAccessMode.Blocked,
            },

            Cache = new CacheDebugInfo
            {
                HasCachedSnapshot  = cachedSnapshot is not null,
                CacheVersion       = cachedSnapshot?.Version,
                CacheEvaluatedAt   = cachedSnapshot?.EvaluatedAtUtc,
                CachedAccessMode   = cachedSnapshot?.AccessMode.ToString(),
            },

            Features = new FeaturesDebugInfo
            {
                PlanCode        = featureSnapshot.PlanCode,
                EnabledModules  = featureSnapshot.EnabledModules.ToList(),
                EnabledFeatures = featureSnapshot.EnabledFeatures.ToList(),
                QuotaLimits     = featureSnapshot.QuotaLimits,
                ResolvedAtUtc   = featureSnapshot.ResolvedAtUtc,
            },

            RecentInvoices = recentInvoices.Select(inv => new InvoiceDebugInfo
            {
                Id            = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                Status        = inv.Status.ToString(),
                Total         = inv.Total,
                CurrencyCode  = inv.CurrencyCode,
                IssuedAtUtc   = inv.IssuedAtUtc,
                DueAtUtc      = inv.DueAtUtc,
                PaidAtUtc     = inv.PaidAtUtc,
            }).ToList(),

            RecentBillingEvents = recentEvents.Select(e => new BillingEventDebugInfo
            {
                EventType      = e.EventType,
                Source         = e.Source.ToString(),
                OccurredAtUtc  = e.OccurredAtUtc,
                CorrelationId  = e.CorrelationId,
            }).ToList(),
        };

        return this.ApiOk(snapshot);
    }
}

// ── Debug DTOs ────────────────────────────────────────────────────────────────

public sealed class SaasDebugSnapshot
{
    public Guid     SubscriberId      { get; init; }
    public string   SubscriberName    { get; init; } = null!;
    public string   SubscriberSlug    { get; init; } = null!;
    public string?  PlanCode          { get; init; }
    public string   PreferredLanguage { get; init; } = null!;
    public DateTime CheckedAtUtc      { get; init; }

    public LifecycleDebugInfo          Lifecycle           { get; init; } = null!;
    public BillingDebugInfo?           Billing             { get; init; }
    public AccessDebugInfo             Access              { get; init; } = null!;
    public CacheDebugInfo              Cache               { get; init; } = null!;
    public FeaturesDebugInfo           Features            { get; init; } = null!;
    public List<InvoiceDebugInfo>      RecentInvoices      { get; init; } = [];
    public List<BillingEventDebugInfo> RecentBillingEvents { get; init; } = [];
}

public sealed class LifecycleDebugInfo
{
    public string   Status          { get; init; } = null!;
    public bool     IsActive        { get; init; }
    public bool     IsOperational   { get; init; }
    public string?  SuspendedReason { get; init; }
    public DateTime? SuspendedAtUtc { get; init; }
}

public sealed class BillingDebugInfo
{
    public string    Status               { get; init; } = null!;
    public string    RenewalState         { get; init; } = null!;
    public string    TrialState           { get; init; } = null!;
    public string    BillingEmail         { get; init; } = null!;
    public string    CurrencyCode         { get; init; } = null!;
    public string    PrimaryProvider      { get; init; } = null!;
    public bool      AllowsPlatformAccess { get; init; }
    public DateTime? TrialEndsAtUtc       { get; init; }
    public DateTime? GracePeriodEndsAtUtc { get; init; }
    public DateTime? CurrentPeriodEndUtc  { get; init; }
}

public sealed class AccessDebugInfo
{
    public string  AccessMode   { get; init; } = null!;
    public string  DenialReason { get; init; } = null!;
    public string? DenialCode   { get; init; }
    public bool    CanAccess    { get; init; }
    public bool    IsReadOnly   { get; init; }
    public bool    IsBlocked    { get; init; }
}

public sealed class CacheDebugInfo
{
    public bool      HasCachedSnapshot { get; init; }
    public long?     CacheVersion      { get; init; }
    public DateTime? CacheEvaluatedAt  { get; init; }
    public string?   CachedAccessMode  { get; init; }
}

public sealed class FeaturesDebugInfo
{
    public string?                         PlanCode        { get; init; }
    public List<string>                    EnabledModules  { get; init; } = [];
    public List<string>                    EnabledFeatures { get; init; } = [];
    public IReadOnlyDictionary<string,int> QuotaLimits     { get; init; } = new Dictionary<string,int>();
    public DateTime                        ResolvedAtUtc   { get; init; }
}

public sealed class InvoiceDebugInfo
{
    public Guid      Id            { get; init; }
    public string    InvoiceNumber { get; init; } = null!;
    public string    Status        { get; init; } = null!;
    public decimal   Total         { get; init; }
    public string    CurrencyCode  { get; init; } = null!;
    public DateTime? IssuedAtUtc   { get; init; }
    public DateTime? DueAtUtc      { get; init; }
    public DateTime? PaidAtUtc     { get; init; }
}

public sealed class BillingEventDebugInfo
{
    public string    EventType     { get; init; } = null!;
    public string    Source        { get; init; } = null!;
    public DateTime  OccurredAtUtc { get; init; }
    public string?   CorrelationId { get; init; }
}
