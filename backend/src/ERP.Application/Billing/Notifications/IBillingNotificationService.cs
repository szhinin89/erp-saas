namespace ERP.Application.Billing.Notifications;

/// <summary>
/// Billing notification contract — email/SMS/push for billing lifecycle events.
/// Implementations: NullBillingNotificationService (default), SendGridNotificationService (future).
/// NEVER call email/SMS APIs directly from handlers — always use this interface.
/// </summary>
public interface IBillingNotificationService
{
    Task NotifyTrialEndingSoonAsync(TrialEndingSoonNotification notification, CancellationToken ct = default);
    Task NotifyPaymentFailedAsync(PaymentFailedNotification notification, CancellationToken ct = default);
    Task NotifyGracePeriodStartedAsync(GracePeriodStartedNotification notification, CancellationToken ct = default);
    Task NotifySubscriptionSuspendedAsync(SubscriptionSuspendedNotification notification, CancellationToken ct = default);
    Task NotifySubscriptionRenewedAsync(SubscriptionRenewedNotification notification, CancellationToken ct = default);
    Task NotifyPaymentSucceededAsync(PaymentSucceededNotification notification, CancellationToken ct = default);
}

// ── Notification payloads ──────────────────────────────────────────────────

public sealed record TrialEndingSoonNotification(
    Guid     SubscriberId,
    string   BillingEmail,
    string?  SubscriberName,
    DateTime TrialEndsAtUtc,
    int      DaysRemaining);

public sealed record PaymentFailedNotification(
    Guid     SubscriberId,
    string   BillingEmail,
    string?  SubscriberName,
    decimal  Amount,
    string   CurrencyCode,
    string?  FailureReason,
    int      AttemptNumber,
    DateTime GracePeriodEndsAtUtc);

public sealed record GracePeriodStartedNotification(
    Guid     SubscriberId,
    string   BillingEmail,
    string?  SubscriberName,
    DateTime GracePeriodEndsAtUtc);

public sealed record SubscriptionSuspendedNotification(
    Guid    SubscriberId,
    string  BillingEmail,
    string? SubscriberName,
    string? Reason);

public sealed record SubscriptionRenewedNotification(
    Guid     SubscriberId,
    string   BillingEmail,
    string?  SubscriberName,
    decimal  Amount,
    string   CurrencyCode,
    DateTime PeriodEndUtc);

public sealed record PaymentSucceededNotification(
    Guid     SubscriberId,
    string   BillingEmail,
    string?  SubscriberName,
    decimal  Amount,
    string   CurrencyCode,
    string?  InvoiceNumber);
