namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Minimal, cacheable access decision for a subscriber.
/// Contains ONLY what is needed to make an access decision — no EF entities,
/// no navigation properties, no modules, no permissions.
/// Serialized to Redis/MemoryCache as JSON.
/// </summary>
public sealed class SubscriptionAccessSnapshot
{
    public Guid                          SubscriberId          { get; init; }
    public SubscriptionAccessMode        AccessMode            { get; init; }
    public SubscriptionAccessDenialReason DenialReason         { get; init; }
    public string?                       DenialCode            { get; init; }
    public string?                       PlanCode              { get; init; }
    public DateTime?                     TrialEndsAtUtc        { get; init; }
    public DateTime?                     GracePeriodEndsAtUtc  { get; init; }

    /// <summary>Monotonically-increasing version used for versioned cache keys.</summary>
    public long   Version        { get; init; }

    /// <summary>UTC timestamp when this snapshot was built from the database.</summary>
    public DateTime EvaluatedAtUtc { get; init; }

    // ── Computed helpers ──────────────────────────────────────────────────────

    public bool CanAccess  => AccessMode != SubscriptionAccessMode.Blocked;
    public bool IsReadOnly => AccessMode == SubscriptionAccessMode.ReadOnly;
    public bool IsBlocked  => AccessMode == SubscriptionAccessMode.Blocked;

    /// <summary>Converts snapshot to <see cref="SubscriptionAccessResult"/> for backward compatibility.</summary>
    public SubscriptionAccessResult ToResult() => AccessMode switch
    {
        SubscriptionAccessMode.FullAccess => SubscriptionAccessResult.Allow(),
        SubscriptionAccessMode.ReadOnly   => DenialReason switch
        {
            SubscriptionAccessDenialReason.BillingPastDue     => SubscriptionAccessResult.PastDue("Pago pendiente."),
            SubscriptionAccessDenialReason.GracePeriodExpired => SubscriptionAccessResult.GracePeriod("Período de gracia activo."),
            _                                                  => SubscriptionAccessResult.PastDue("Pago pendiente."),
        },
        _ => DenialReason switch
        {
            SubscriptionAccessDenialReason.Suspended          => SubscriptionAccessResult.Suspended("Cuenta suspendida."),
            SubscriptionAccessDenialReason.Cancelled          => SubscriptionAccessResult.Cancelled("Suscripción cancelada."),
            SubscriptionAccessDenialReason.TrialExpired       => SubscriptionAccessResult.TrialExpired("Período de prueba vencido."),
            SubscriptionAccessDenialReason.GracePeriodExpired => SubscriptionAccessResult.TrialExpired("Período de gracia vencido."),
            SubscriptionAccessDenialReason.Inactive           => SubscriptionAccessResult.Inactive("Cuenta inactiva."),
            _                                                  => SubscriptionAccessResult.Suspended("Acceso denegado."),
        },
    };
}
