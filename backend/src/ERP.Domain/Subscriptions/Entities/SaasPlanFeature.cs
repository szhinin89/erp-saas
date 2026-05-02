namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Inclusión de una feature en un plan con límite opcional (null = ilimitado).</summary>
public sealed class SaasPlanFeature
{
    public Guid Id { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool IsIncluded { get; private set; }
    /// <summary>Límite por periodo de facturación/uso; null = sin límite.</summary>
    public long? LimitPerPeriod { get; private set; }

    private SaasPlanFeature() { }

    public static SaasPlanFeature Create(Guid planId, Guid featureId, bool isIncluded, long? limitPerPeriod)
    {
        return new SaasPlanFeature
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            FeatureId = featureId,
            IsIncluded = isIncluded,
            LimitPerPeriod = limitPerPeriod,
        };
    }
}
