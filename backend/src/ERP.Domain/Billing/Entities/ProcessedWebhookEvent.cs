using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>
/// Deduplication record for processed webhook events.
/// Prevents double-processing when payment providers retry delivery.
/// Immutable after creation. SubscriberId = Guid.Empty for events with no tenant match.
/// </summary>
public sealed class ProcessedWebhookEvent : AuditableEntity
{
    public const int ProviderEventIdMaxLen = 200;
    public const int RawEventTypeMaxLen    = 100;

    public PaymentProviderType ProviderType    { get; private set; }
    public string              ProviderEventId { get; private set; } = null!;
    public string              RawEventType    { get; private set; } = null!;
    public DateTime            ProcessedAtUtc  { get; private set; }

    private ProcessedWebhookEvent() { }

    public static ProcessedWebhookEvent Record(
        PaymentProviderType providerType,
        string              providerEventId,
        string              rawEventType,
        Guid                subscriberId)
    {
        var e = new ProcessedWebhookEvent
        {
            Id              = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            ProviderType    = providerType,
            ProviderEventId = providerEventId.Trim(),
            RawEventType    = rawEventType.Trim(),
            ProcessedAtUtc  = DateTime.UtcNow,
        };
        e.SetCreated(subscriberId == Guid.Empty ? Guid.Empty : subscriberId);
        return e;
    }
}
