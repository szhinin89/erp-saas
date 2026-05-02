namespace ERP.Domain.Subscriptions.Interfaces;

/// <summary>
/// Servicio de aplicación de suscripciones SaaS (features y límites). Implementación en Infrastructure.
/// Separado de permisos de seguridad (auth).
/// </summary>
public interface ISubscriptionService
{
    /// <summary>Indica si el tenant tiene habilitada la feature comercial.</summary>
    Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default);

    /// <summary>
    /// Comprueba si el tenant puede consumir <paramref name="amount"/> unidades adicionales
    /// respecto al límite del periodo actual (feature medida). Si el límite es null → ilimitado.
    /// </summary>
    Task<bool> CheckLimitAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default);

    /// <summary>Incrementa el consumo acumulado para la feature en el periodo actual.</summary>
    Task IncrementUsageAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default);
}
