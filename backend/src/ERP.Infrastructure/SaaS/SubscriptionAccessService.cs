using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Single source of truth for platform access decisions.
/// Evaluates Subscriber lifecycle + BillingAccount status together.
/// </summary>
public sealed class SubscriptionAccessService : ISubscriptionAccessService
{
    private readonly ISubscriberRepository       _subscribers;
    private readonly ISubscriberBillingRepository _billing;

    public SubscriptionAccessService(
        ISubscriberRepository subscribers,
        ISubscriberBillingRepository billing)
    {
        _subscribers = subscribers;
        _billing     = billing;
    }

    public async Task<SubscriptionAccessResult> EvaluateAsync(
        Guid subscriberId, CancellationToken ct = default)
    {
        // Anonymous / system calls always allowed
        if (subscriberId == Guid.Empty)
            return SubscriptionAccessResult.Allow();

        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);
        if (subscriber is null)
            return SubscriptionAccessResult.Inactive("Suscriptor no encontrado.");

        // Subscriber-level hard blocks
        switch (subscriber.LifecycleStatus)
        {
            case SubscriberLifecycleStatus.Suspended:
                return SubscriptionAccessResult.Suspended(
                    $"Cuenta suspendida. {subscriber.SuspendedReason}".Trim());

            case SubscriberLifecycleStatus.Inactive:
                return SubscriptionAccessResult.Inactive("Cuenta inactiva.");
        }

        // Billing-level checks
        var account = await _billing.GetAccountBySubscriberIdAsync(subscriberId, ct);
        if (account is null)
            return SubscriptionAccessResult.Allow(); // no billing account = pre-provisioning, allow

        var utcNow = DateTime.UtcNow;

        switch (account.Status)
        {
            case BillingAccountStatus.Cancelled:
                return SubscriptionAccessResult.Cancelled("Suscripción cancelada.");

            case BillingAccountStatus.Suspended:
                return SubscriptionAccessResult.Suspended(
                    "Cuenta de facturación suspendida.");

            case BillingAccountStatus.GracePeriod:
                if (account.GracePeriodEndsAtUtc.HasValue && account.GracePeriodEndsAtUtc.Value < utcNow)
                    return SubscriptionAccessResult.TrialExpired(
                        $"Período de gracia vencido ({account.GracePeriodEndsAtUtc:O}).");

                return SubscriptionAccessResult.GracePeriod(
                    $"Período de gracia activo hasta {account.GracePeriodEndsAtUtc:O}.");

            case BillingAccountStatus.Trialing:
                if (account.TrialEndsAtUtc.HasValue && account.TrialEndsAtUtc.Value < utcNow)
                    return SubscriptionAccessResult.TrialExpired(
                        $"Período de prueba vencido ({account.TrialEndsAtUtc:O}).");
                return SubscriptionAccessResult.Allow();

            case BillingAccountStatus.PastDue:
                return SubscriptionAccessResult.PastDue("Pago pendiente.");

            default: // Active
                return SubscriptionAccessResult.Allow();
        }
    }
}
