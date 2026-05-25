using ERP.Domain.Common;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>
/// Ajuste por tenant (add-on o override de límite) sobre la suscripción.
/// </summary>
public sealed class SubscriptionFeatureOverride : AuditableEntity
{
    public Guid SubscriptionId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool IsEnabled { get; private set; }
    /// <summary>Override del límite por periodo; null = usar el del plan.</summary>
    public long? LimitOverridePerPeriod { get; private set; }

    private SubscriptionFeatureOverride() { }

    public static SubscriptionFeatureOverride Create(
        Guid subscriberId,
        Guid subscriptionId,
        Guid featureId,
        bool isEnabled,
        long? limitOverridePerPeriod,
        Guid createdBy)
    {
        var o = new SubscriptionFeatureOverride
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            SubscriptionId = subscriptionId,
            FeatureId = featureId,
            IsEnabled = isEnabled,
            LimitOverridePerPeriod = limitOverridePerPeriod,
        };
        o.SetCreated(createdBy);
        return o;
    }

    public void SetEnabled(bool isEnabled, Guid updatedBy)
    {
        IsEnabled = isEnabled;
        SetUpdated(updatedBy);
    }
}
