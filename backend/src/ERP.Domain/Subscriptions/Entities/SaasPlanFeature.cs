namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Inclusión de una feature en un plan con límite opcional (null = ilimitado).</summary>
public sealed class CommercialPlanFeature
{
    public Guid Id { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool IsIncluded { get; private set; }
    /// <summary>Límite por periodo de facturación/uso; null = sin límite.</summary>
    public long? LimitPerPeriod { get; private set; }

    private CommercialPlanFeature() { }

    public static CommercialPlanFeature Create(Guid planId, Guid featureId, bool isIncluded, long? limitPerPeriod)
    {
        return new CommercialPlanFeature
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            FeatureId = featureId,
            IsIncluded = isIncluded,
            LimitPerPeriod = limitPerPeriod,
        };
    }

    public void SetInclusion(bool isIncluded, long? limitPerPeriod)
    {
        IsIncluded = isIncluded;
        LimitPerPeriod = limitPerPeriod;
    }
}
