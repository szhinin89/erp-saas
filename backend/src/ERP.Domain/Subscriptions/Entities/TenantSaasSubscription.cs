using ERP.Domain.Common;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Suscripción activa de un tenant a un plan comercial.</summary>
public sealed class TenantSaasSubscription : AuditableEntity
{
    public Guid PlanId { get; private set; }
    public TenantSubscriptionStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CurrentPeriodEndUtc { get; private set; }

    private TenantSaasSubscription() { }

    public static TenantSaasSubscription Create(
        Guid tenantId,
        Guid planId,
        Guid createdBy,
        TenantSubscriptionStatus status = TenantSubscriptionStatus.Active,
        DateTime? currentPeriodEndUtc = null)
    {
        var s = new TenantSaasSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
        Status = TenantSubscriptionStatus.Cancelled;
        SetUpdated(updatedBy);
    }
}
