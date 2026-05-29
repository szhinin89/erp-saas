using ERP.Application.Billing.Notifications;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// No-op notification service — logs but does not send emails/SMS.
/// Replace with SendGridNotificationService/TwilioNotificationService when ready.
/// </summary>
public sealed class NullBillingNotificationService : IBillingNotificationService
{
    private readonly ILogger<NullBillingNotificationService> _log;

    public NullBillingNotificationService(ILogger<NullBillingNotificationService> log) => _log = log;

    public Task NotifyTrialEndingSoonAsync(TrialEndingSoonNotification n, CancellationToken ct = default)
    {
        _log.LogInformation("[NOTIFY-NULL] TrialEndingSoon subscriber={Id} days={Days} email={Email}",
            n.SubscriberId, n.DaysRemaining, n.BillingEmail);
        return Task.CompletedTask;
    }

    public Task NotifyPaymentFailedAsync(PaymentFailedNotification n, CancellationToken ct = default)
    {
        _log.LogWarning("[NOTIFY-NULL] PaymentFailed subscriber={Id} attempt={Attempt} email={Email}",
            n.SubscriberId, n.AttemptNumber, n.BillingEmail);
        return Task.CompletedTask;
    }

    public Task NotifyGracePeriodStartedAsync(GracePeriodStartedNotification n, CancellationToken ct = default)
    {
        _log.LogWarning("[NOTIFY-NULL] GracePeriodStarted subscriber={Id} ends={End} email={Email}",
            n.SubscriberId, n.GracePeriodEndsAtUtc, n.BillingEmail);
        return Task.CompletedTask;
    }

    public Task NotifySubscriptionSuspendedAsync(SubscriptionSuspendedNotification n, CancellationToken ct = default)
    {
        _log.LogWarning("[NOTIFY-NULL] SubscriptionSuspended subscriber={Id} email={Email}",
            n.SubscriberId, n.BillingEmail);
        return Task.CompletedTask;
    }

    public Task NotifySubscriptionRenewedAsync(SubscriptionRenewedNotification n, CancellationToken ct = default)
    {
        _log.LogInformation("[NOTIFY-NULL] SubscriptionRenewed subscriber={Id} amount={Amount} email={Email}",
            n.SubscriberId, n.Amount, n.BillingEmail);
        return Task.CompletedTask;
    }

    public Task NotifyPaymentSucceededAsync(PaymentSucceededNotification n, CancellationToken ct = default)
    {
        _log.LogInformation("[NOTIFY-NULL] PaymentSucceeded subscriber={Id} amount={Amount} email={Email}",
            n.SubscriberId, n.Amount, n.BillingEmail);
        return Task.CompletedTask;
    }
}
