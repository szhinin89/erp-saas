namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Structured reason why access was denied or downgraded.
/// Allows the frontend to show specific, actionable messages.
/// </summary>
public enum SubscriptionAccessDenialReason
{
    None               = 0,
    Suspended          = 1,
    Cancelled          = 2,
    TrialExpired       = 3,
    GracePeriodExpired = 4,
    BillingPastDue     = 5,
    Inactive           = 6,
}
