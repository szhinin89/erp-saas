using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Routes validated+parsed webhook events to the appropriate lifecycle or billing handler.
/// Provider-agnostic: receives ProviderWebhookEvent regardless of whether it came from Stripe/Kushki/etc.
///
/// Event routing:
///   PaymentSucceeded    → activate (if Suspended/GracePeriod) + mark invoice paid
///   PaymentFailed       → enter grace period
///   SubscriptionCancelled → cancel subscription
///   InvoicePaid         → mark invoice paid + advance period
/// </summary>
public sealed class WebhookEventProcessor : IPaymentWebhookProcessor
{
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly ILogger<WebhookEventProcessor>     _log;

    public WebhookEventProcessor(
        ISubscriptionLifecycleOrchestrator lifecycle,
        ILogger<WebhookEventProcessor> log)
    {
        _lifecycle = lifecycle;
        _log       = log;
    }

    public async Task ProcessAsync(ProviderWebhookEvent evt, CancellationToken ct = default)
    {
        if (evt.SubscriberId is null)
        {
            _log.LogWarning(
                "WebhookEventProcessor: no subscriber resolved for event {Id} type={Type}",
                evt.ProviderEventId, evt.EventType);
            return;
        }

        var subscriberId = evt.SubscriberId.Value;
        var actorId      = SubscriptionLifecycleOrchestrator.SystemActorId;

        _log.LogInformation(
            "WebhookEventProcessor: event={EventType} subscriber={SubscriberId} provider={Provider}",
            evt.EventType, subscriberId, evt.ProviderType);

        switch (evt.EventType)
        {
            case ProviderWebhookEventType.PaymentSucceeded:
            case ProviderWebhookEventType.InvoicePaid:
                // Payment received → activate if blocked
                await _lifecycle.ActivateAsync(subscriberId, actorId,
                    $"Payment received via {evt.ProviderType} (ref:{evt.ExternalInvoiceId})", ct);
                break;

            case ProviderWebhookEventType.PaymentFailed:
            case ProviderWebhookEventType.InvoicePaymentFailed:
                // Payment failed → enter grace period (7 days default)
                await _lifecycle.EnterGracePeriodAsync(subscriberId,
                    DateTime.UtcNow.AddDays(7), actorId, ct);
                break;

            case ProviderWebhookEventType.SubscriptionCancelled:
                await _lifecycle.CancelAsync(subscriberId,
                    $"Cancelled via {evt.ProviderType} webhook", actorId, ct);
                break;

            case ProviderWebhookEventType.SubscriptionPastDue:
                // Past due → still allow access but record
                _log.LogWarning(
                    "WebhookEventProcessor: subscriber={SubscriberId} PastDue from {Provider}",
                    subscriberId, evt.ProviderType);
                break;

            default:
                _log.LogDebug(
                    "WebhookEventProcessor: unhandled event type={EventType} for subscriber={SubscriberId}",
                    evt.EventType, subscriberId);
                break;
        }
    }
}
