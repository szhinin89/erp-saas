namespace ERP.Domain.Subscriptions.Entities;

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Cancelled = 2,
    Suspended = 3,
    GracePeriod = 4,
    Trial = 5,
}
