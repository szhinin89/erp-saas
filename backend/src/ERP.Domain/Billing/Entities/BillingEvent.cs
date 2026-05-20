using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>Auditoría inmutable de eventos de billing SaaS (governance + webhooks futuros).</summary>
public sealed class BillingEvent : AuditableEntity
{
    public const int EventTypeMaxLen = 64;
    public const int CorrelationIdMaxLen = 64;
    public const int PayloadMaxLen = 8000;

    public string EventType { get; private set; } = null!;
    public BillingEventSource Source { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? PayloadJson { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? ActorUserId { get; private set; }

    private BillingEvent() { }

    public static BillingEvent Record(
        Guid subscriberId,
        string eventType,
        BillingEventSource source,
        Guid actorUserId,
        string? payloadJson = null,
        string? correlationId = null)
    {
        var e = new BillingEvent
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            EventType = eventType.Trim(),
            Source = source,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? null : payloadJson.Trim(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = actorUserId == Guid.Empty ? null : actorUserId,
        };
        e.SetCreated(actorUserId == Guid.Empty ? Guid.Empty : actorUserId);
        return e;
    }

    public static class Types
    {
        public const string AccountCreated = "billing.account.created";
        public const string AccountUpdated = "billing.account.updated";
        public const string GracePeriodStarted = "billing.grace_period.started";
        public const string Suspended = "billing.suspended";
        public const string Reactivated = "billing.reactivated";
        public const string PlanUpgradeScheduled = "billing.plan.upgrade.scheduled";
        public const string PlanDowngradeScheduled = "billing.plan.downgrade.scheduled";
        public const string ProviderCustomerLinked = "billing.provider.customer.linked";
        public const string InvoiceIssued = "billing.invoice.issued";
        public const string InvoicePaid = "billing.invoice.paid";
    }
}
