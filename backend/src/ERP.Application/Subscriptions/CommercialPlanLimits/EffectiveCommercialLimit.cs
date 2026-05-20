namespace ERP.Application.Subscriptions.CommercialPlanLimits;

/// <summary>Límite efectivo resuelto desde <c>commercial_plan_limits</c> (más overrides futuros).</summary>
public sealed record EffectiveCommercialLimit(
    string LimitCode,
    long LimitValue,
    string PeriodType,
    bool IsHardLimit,
    Guid CommercialPlanId,
    string? PlanCode)
{
    /// <summary><c>LimitValue &lt;= 0</c> significa sin tope numérico.</summary>
    public bool IsUnlimited => LimitValue <= 0;
}
