namespace Platform.Contracts.Webhooks;

/// <summary>
/// Generic envelope used to deliver an integration event payload to Platform via webhook.
/// </summary>
public sealed record WebhookEnvelope<TPayload>(
    Guid EventId,
    WebhookEventType EventType,
    DateTime OccurredOn,
    Guid TenantId,
    TPayload Payload
);
