using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Routes validated+parsed webhook events to lifecycle and billing handlers.
/// DEDUPLICATION: processed_webhook_events table prevents double-processing on retries.
/// </summary>
public sealed class WebhookEventProcessor : IPaymentWebhookProcessor
{
    private readonly ISubscriptionLifecycleOrchestrator _lifecycle;
    private readonly ISubscriberBillingRepository       _billing;
    private readonly SubscriptionBillingOptions         _opts;
    private readonly ILogger<WebhookEventProcessor>     _log;

    public WebhookEventProcessor(
        ISubscriptionLifecycleOrchestrator lifecycle,
        ISubscriberBillingRepository billing,
        IOptions<SubscriptionBillingOptions> options,
        ILogger<WebhookEventProcessor> log)
    {
        _lifecycle = lifecycle;
        _billing   = billing;
        _opts      = options.Value;
        _log       = log;
    }

    public async Task ProcessAsync(ProviderWebhookEvent evt, CancellationToken ct = default)
    {
        // Deduplication: skip if already processed
        if (await _billing.IsWebhookEventProcessedAsync(evt.ProviderEventId, ct))
        {
            _log.LogDebug(
                "WebhookEventProcessor: duplicate event {EventId} type={Type} — skipped",
                evt.ProviderEventId, evt.EventType);
            return;
        }

        var subscriberId = evt.SubscriberId ?? Guid.Empty;

        _log.LogInformation(
            "WebhookEventProcessor: event={EventType} id={EventId} subscriber={SubscriberId}",
            evt.EventType, evt.ProviderEventId, subscriberId);

        try
        {
            await DispatchAsync(evt, subscriberId, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "WebhookEventProcessor: error processing event {EventId} type={Type}",
                evt.ProviderEventId, evt.EventType);
            throw; // provider retries on 500
        }

        // Mark processed only on success — prevents marking on partial failure
        await _billing.AddProcessedWebhookEventAsync(
            ProcessedWebhookEvent.Record(evt.ProviderType, evt.ProviderEventId, evt.RawEventType, subscriberId),
            ct);
        await _billing.SaveChangesAsync(ct);
    }

    private async Task DispatchAsync(ProviderWebhookEvent evt, Guid subscriberId, CancellationToken ct)
    {
        if (subscriberId == Guid.Empty)
        {
            _log.LogWarning(
                "WebhookEventProcessor: no subscriber for event {Id} type={Type} — ignoring",
                evt.ProviderEventId, evt.EventType);
            return;
        }

        var actorId = SubscriptionLifecycleOrchestrator.SystemActorId;

        switch (evt.EventType)
        {
            case ProviderWebhookEventType.PaymentSucceeded:
            case ProviderWebhookEventType.InvoicePaid:
                await _lifecycle.ActivateAsync(subscriberId, actorId,
                    $"Payment via {evt.ProviderType} (ref:{evt.ExternalInvoiceId})", ct);
                break;

            case ProviderWebhookEventType.PaymentFailed:
            case ProviderWebhookEventType.InvoicePaymentFailed:
                await _lifecycle.EnterGracePeriodAsync(
                    subscriberId, DateTime.UtcNow.AddDays(_opts.WebhookPaymentFailureGracePeriodDays), actorId, ct);
                break;

            case ProviderWebhookEventType.SubscriptionCancelled:
                await _lifecycle.CancelAsync(subscriberId,
                    $"Cancelled via {evt.ProviderType} webhook", actorId, ct);
                break;

            case ProviderWebhookEventType.SubscriptionPastDue:
                _log.LogWarning(
                    "WebhookEventProcessor: subscriber={Id} PastDue — renewal engine will handle", subscriberId);
                break;

            default:
                _log.LogDebug(
                    "WebhookEventProcessor: unhandled event type={Type}", evt.EventType);
                break;
        }
    }
}
