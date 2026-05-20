namespace ERP.Application.Subscriptions.CommercialPlanLimits;

/// <summary>
/// Snapshot cache-ready de límites comerciales del suscriptor (Redis futuro: clave <c>limits:{subscriberId}</c>).
/// </summary>
public sealed record CommercialLimitsSnapshot(
    Guid SubscriberId,
    Guid? CommercialPlanId,
    string? PlanCode,
    IReadOnlyDictionary<string, EffectiveCommercialLimit> LimitsByCode,
    DateTime ResolvedAtUtc)
{
    public string CacheKey => $"commercial-limits:{SubscriberId:N}";
}
