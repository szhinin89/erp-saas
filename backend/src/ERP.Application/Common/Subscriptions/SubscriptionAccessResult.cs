namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Immutable result of a subscription access evaluation.
/// All access decisions for tenant platform access must go through
/// <see cref="ISubscriptionAccessService.EvaluateAsync"/>.
/// </summary>
public sealed class SubscriptionAccessResult
{
    // ── Backward-compatible boolean flags ────────────────────────────────────
    public bool    CanAccess      { get; init; }
    public bool    IsSuspended    { get; init; }
    public bool    IsInactive     { get; init; }
    public bool    IsTrialExpired { get; init; }
    public bool    IsPastDue      { get; init; }
    public bool    IsGracePeriod  { get; init; }
    public bool    IsCancelled    { get; init; }
    public string? Reason         { get; init; }

    // ── Structured access decision (FASE 7/9) ────────────────────────────────
    public SubscriptionAccessMode         AccessMode   { get; init; } = SubscriptionAccessMode.FullAccess;
    public SubscriptionAccessDenialReason DenialReason { get; init; } = SubscriptionAccessDenialReason.None;

    // ── Factory methods ───────────────────────────────────────────────────────

    public static SubscriptionAccessResult Allow() =>
        new() { CanAccess = true, AccessMode = SubscriptionAccessMode.FullAccess };

    public static SubscriptionAccessResult Suspended(string reason) =>
        new()
        {
            CanAccess    = false, IsSuspended = true, Reason = reason,
            AccessMode   = SubscriptionAccessMode.Blocked,
            DenialReason = SubscriptionAccessDenialReason.Suspended,
        };

    public static SubscriptionAccessResult Inactive(string reason) =>
        new()
        {
            CanAccess    = false, IsInactive = true, Reason = reason,
            AccessMode   = SubscriptionAccessMode.Blocked,
            DenialReason = SubscriptionAccessDenialReason.Inactive,
        };

    public static SubscriptionAccessResult Cancelled(string reason) =>
        new()
        {
            CanAccess    = false, IsCancelled = true, Reason = reason,
            AccessMode   = SubscriptionAccessMode.Blocked,
            DenialReason = SubscriptionAccessDenialReason.Cancelled,
        };

    public static SubscriptionAccessResult GracePeriod(string reason) =>
        new()
        {
            CanAccess    = true, IsGracePeriod = true, Reason = reason,
            AccessMode   = SubscriptionAccessMode.ReadOnly,
            DenialReason = SubscriptionAccessDenialReason.GracePeriodExpired,
        };

    public static SubscriptionAccessResult PastDue(string reason) =>
        new()
        {
            CanAccess    = true, IsPastDue = true, Reason = reason,
            AccessMode   = SubscriptionAccessMode.ReadOnly,
            DenialReason = SubscriptionAccessDenialReason.BillingPastDue,
        };

    public static SubscriptionAccessResult TrialExpired(string reason) =>
        new()
        {
            CanAccess    = false, IsTrialExpired = true, Reason = reason,
            AccessMode   = SubscriptionAccessMode.Blocked,
            DenialReason = SubscriptionAccessDenialReason.TrialExpired,
        };

    /// <summary>Structured denial code for API responses.</summary>
    public string DenialCode => DenialReason switch
    {
        SubscriptionAccessDenialReason.Suspended          => "subscription_suspended",
        SubscriptionAccessDenialReason.Inactive           => "subscription_inactive",
        SubscriptionAccessDenialReason.Cancelled          => "subscription_cancelled",
        SubscriptionAccessDenialReason.TrialExpired       => "subscription_trial_expired",
        SubscriptionAccessDenialReason.GracePeriodExpired => "subscription_grace_period_expired",
        SubscriptionAccessDenialReason.BillingPastDue     => "subscription_past_due",
        _                                                  => "subscription_access_denied",
    };
}
