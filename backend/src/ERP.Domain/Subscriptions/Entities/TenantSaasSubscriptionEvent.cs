using ERP.Domain.Common;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Auditoría de cambios de plan y overrides de suscripción SaaS.</summary>
public sealed class TenantSaasSubscriptionEvent : AuditableEntity
{
    public Guid? SubscriptionId { get; private set; }
    public string EventType { get; private set; } = null!;
    public Guid? PreviousPlanId { get; private set; }
    public Guid? NewPlanId { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private TenantSaasSubscriptionEvent() { }

    public static TenantSaasSubscriptionEvent Create(
        Guid tenantId,
        string eventType,
        Guid actorId,
        Guid? subscriptionId = null,
        Guid? previousPlanId = null,
        Guid? newPlanId = null,
        string? metadataJson = null)
    {
        var e = new TenantSaasSubscriptionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            EventType = eventType.Trim(),
            PreviousPlanId = previousPlanId,
            NewPlanId = newPlanId,
            MetadataJson = metadataJson,
            OccurredAtUtc = DateTime.UtcNow,
        };
        e.SetCreated(actorId);
        return e;
    }

    public static class Types
    {
        public const string PlanAssigned = "plan_assigned";
        public const string PlanChanged = "plan_changed";
        public const string PlanCancelled = "plan_cancelled";
        public const string ModuleOverridesApplied = "module_overrides_applied";
        public const string ModuleOverridesCleared = "module_overrides_cleared";
    }
}
