namespace ERP.Domain.Platform.Audit.Entities;

/// <summary>
/// Registro append-only de acciones realizadas por operadores de plataforma (operador platform).
/// NO filtrado por SubscriberId — es un log global de control plane.
/// Cubre: cambios de plan, suspend/activate, switch-subscriber, login failures, config changes.
/// </summary>
public sealed class PlatformAuditLog
{
    public const int ActorEmailMaxLen = 256;
    public const int ActionMaxLen = 128;
    public const int ResourceTypeMaxLen = 64;
    public const int IpAddressMaxLen = 45;
    public const int CorrelationIdMaxLen = 128;

    public static class Actions
    {
        public const string SubscriberCreated = "subscriber.created";
        public const string SubscriberActivated = "subscriber.activated";
        public const string SubscriberSuspended = "subscriber.suspended";
        public const string SubscriberDeactivated = "subscriber.deactivated";
        public const string PlanChanged = "subscriber.plan_changed";
        public const string TrialStarted = "subscriber.trial_started";
        public const string GracePeriodStarted = "subscriber.grace_period_started";
        public const string LoginSuccess = "platform.login_success";
        public const string LoginFailure = "platform.login_failure";
        public const string SwitchSubscriber = "platform.switch_subscriber";
        public const string ConfigChanged = "platform.config_changed";
        public const string FeatureOverrideSet = "entitlement.feature_override_set";
        public const string FeatureOverrideRemoved = "entitlement.feature_override_removed";
        public const string SessionRevoked = "platform.session_revoked";
    }

    public Guid Id { get; private set; }

    /// <summary>ID del operador de plataforma que realizó la acción (null si automático/sistema).</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>Email del operador (denormalizado para legibilidad en auditoría).</summary>
    public string? ActorEmail { get; private set; }

    /// <summary>Código de acción estable (ver <see cref="Actions"/>).</summary>
    public string Action { get; private set; } = null!;

    /// <summary>Subscriber afectado por la acción (null si es acción global).</summary>
    public Guid? TargetSubscriberId { get; private set; }

    /// <summary>Tipo de entidad afectada (ej. "Subscriber", "CommercialPlan").</summary>
    public string? ResourceType { get; private set; }

    /// <summary>ID de la entidad afectada.</summary>
    public Guid? ResourceId { get; private set; }

    /// <summary>Estado anterior serializado como JSON (nullable — no siempre disponible).</summary>
    public string? OldValueJson { get; private set; }

    /// <summary>Estado nuevo serializado como JSON.</summary>
    public string? NewValueJson { get; private set; }

    /// <summary>IP del cliente (IPv4 o IPv6).</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Correlation/trace ID del request HTTP.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>Descripción libre (motivo de suspensión, notas, etc.).</summary>
    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private PlatformAuditLog() { }

    public static PlatformAuditLog Create(
        string action,
        Guid? actorUserId,
        string? actorEmail,
        Guid? targetSubscriberId,
        string? resourceType = null,
        Guid? resourceId = null,
        string? oldValueJson = null,
        string? newValueJson = null,
        string? ipAddress = null,
        string? correlationId = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action requerida.", nameof(action));

        return new PlatformAuditLog
        {
            Id = Guid.NewGuid(),
            Action = action.Trim().ToLowerInvariant(),
            ActorUserId = actorUserId == Guid.Empty ? null : actorUserId,
            ActorEmail = Truncate(actorEmail, ActorEmailMaxLen),
            TargetSubscriberId = targetSubscriberId == Guid.Empty ? null : targetSubscriberId,
            ResourceType = Truncate(resourceType, ResourceTypeMaxLen),
            ResourceId = resourceId,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            IpAddress = Truncate(ipAddress, IpAddressMaxLen),
            CorrelationId = Truncate(correlationId, CorrelationIdMaxLen),
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    private static string? Truncate(string? value, int maxLen) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLen)];
}
