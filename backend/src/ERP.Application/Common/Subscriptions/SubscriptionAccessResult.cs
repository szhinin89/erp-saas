namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Immutable result of a subscription access evaluation.
/// All access decisions for tenant platform access must go through
/// <see cref="ISubscriptionAccessService.EvaluateAsync"/>.
/// </summary>
public sealed class SubscriptionAccessResult
{
    public bool    CanAccess     { get; init; }
    public bool    IsSuspended   { get; init; }
    public bool    IsInactive    { get; init; }
    public bool    IsTrialExpired { get; init; }
    public bool    IsPastDue     { get; init; }
    public bool    IsGracePeriod { get; init; }
    public bool    IsCancelled   { get; init; }
    public string? Reason        { get; init; }

    public static SubscriptionAccessResult Allow() =>
        new() { CanAccess = true };

    public static SubscriptionAccessResult Suspended(string reason) =>
        new() { CanAccess = false, IsSuspended = true, Reason = reason };

    public static SubscriptionAccessResult Inactive(string reason) =>
        new() { CanAccess = false, IsInactive = true, Reason = reason };

    public static SubscriptionAccessResult Cancelled(string reason) =>
        new() { CanAccess = false, IsCancelled = true, Reason = reason };

    public static SubscriptionAccessResult GracePeriod(string reason) =>
        new() { CanAccess = true, IsGracePeriod = true, Reason = reason };

    public static SubscriptionAccessResult PastDue(string reason) =>
        new() { CanAccess = true, IsPastDue = true, Reason = reason };

    public static SubscriptionAccessResult TrialExpired(string reason) =>
        new() { CanAccess = false, IsTrialExpired = true, Reason = reason };

    /// <summary>Denial code for structured API error response.</summary>
    public string DenialCode => this switch
    {
        { IsSuspended   : true } => "subscription_suspended",
        { IsInactive    : true } => "subscription_inactive",
        { IsCancelled   : true } => "subscription_cancelled",
        { IsTrialExpired: true } => "subscription_trial_expired",
        _                        => "subscription_access_denied",
    };
}
