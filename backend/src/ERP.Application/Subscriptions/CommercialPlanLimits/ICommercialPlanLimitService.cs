namespace ERP.Application.Subscriptions.CommercialPlanLimits;

/// <summary>
/// Motor centralizado de enforcement de <c>commercial_plan_limits</c>.
/// No validar límites en handlers ni controllers — usar este servicio.
/// </summary>
public interface ICommercialPlanLimitService
{
    Task<CommercialLimitsSnapshot> GetEffectiveLimitsSnapshotAsync(
        Guid subscriberId,
        CancellationToken ct = default);

    Task<CommercialLimitEvaluationResult> EvaluateAsync(
        Guid subscriberId,
        string limitCode,
        long increment = 1,
        CancellationToken ct = default);

    /// <summary>
    /// Ejecuta <paramref name="action"/> dentro de transacción serializable con bloqueo del suscriptor,
    /// re-evaluando el límite antes de persistir (concurrencia segura).
    /// </summary>
    Task ExecuteWithLimitEnforcementAsync(
        Guid subscriberId,
        string limitCode,
        long increment,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default);

    /// <summary>Lanza <see cref="Domain.Subscriptions.Exceptions.CommercialPlanLimitExceededException"/> si no se permite el incremento.</summary>
    Task EnsureCanIncrementAsync(
        Guid subscriberId,
        string limitCode,
        long increment = 1,
        CancellationToken ct = default);
}
