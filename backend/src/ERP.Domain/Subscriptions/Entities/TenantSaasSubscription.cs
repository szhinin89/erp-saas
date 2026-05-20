using ERP.Domain.Common;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Suscripción activa de un tenant a un plan comercial.</summary>
public sealed class SubscriberSubscription : AuditableEntity
{
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CurrentPeriodEndUtc { get; private set; }

    private SubscriberSubscription() { }

    public static SubscriberSubscription Create(
        Guid subscriberId,
        Guid planId,
        Guid createdBy,
        SubscriptionStatus status = SubscriptionStatus.Active,
        DateTime? currentPeriodEndUtc = null)
    {
        var s = new SubscriberSubscription
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            PlanId = planId,
            Status = status,
            StartedAtUtc = DateTime.UtcNow,
            CurrentPeriodEndUtc = currentPeriodEndUtc,
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void Cancel(Guid updatedBy)
    {
        Status = SubscriptionStatus.Cancelled;
        SetUpdated(updatedBy);
    }

    /// <summary>Cambia de plan sin borrar la fila (sync / SuperAdmin).</summary>
    public void ReassignPlan(Guid planId, Guid updatedBy)
    {
        PlanId = planId;
        if (Status != SubscriptionStatus.Active)
            Status = SubscriptionStatus.Active;
        SetUpdated(updatedBy);
    }
}
