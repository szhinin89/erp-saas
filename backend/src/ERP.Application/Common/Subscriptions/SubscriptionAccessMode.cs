namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Granular access level for the subscriber platform session.
/// Replaces binary CanAccess to support future ReadOnly enforcement.
/// </summary>
public enum SubscriptionAccessMode
{
    /// <summary>Full read-write access. Active or Trialing subscriber.</summary>
    FullAccess = 1,

    /// <summary>Read-only access. PastDue or GracePeriod subscriber — can browse but not create.</summary>
    ReadOnly   = 2,

    /// <summary>No access. Suspended, Cancelled, Inactive, or TrialExpired subscriber.</summary>
    Blocked    = 3,
}
