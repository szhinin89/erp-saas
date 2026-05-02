using ERP.Domain.Common;

namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Consumo acumulado de una feature medida por periodo (p. ej. mes calendario).</summary>
public sealed class TenantSubscriptionUsage : AuditableEntity
{
    public Guid FeatureId { get; private set; }
    /// <summary>Clave de periodo estable (ej. <c>2026-05</c> mensual UTC).</summary>
    public string PeriodKey { get; private set; } = null!;
    public long Quantity { get; private set; }

    private TenantSubscriptionUsage() { }

    public static TenantSubscriptionUsage Create(
        Guid tenantId,
        Guid featureId,
        string periodKey,
        long initialQuantity,
        Guid createdBy)
    {
        var pk = (periodKey ?? string.Empty).Trim();
        if (pk.Length == 0 || pk.Length > 32)
            throw new ArgumentException("PeriodKey inválido.", nameof(periodKey));

        var u = new TenantSubscriptionUsage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FeatureId = featureId,
            PeriodKey = pk,
            Quantity = initialQuantity,
        };
        u.SetCreated(createdBy);
        return u;
    }

    public void AddQuantity(long amount, Guid updatedBy)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Quantity += amount;
        SetUpdated(updatedBy);
    }
}
