using ERP.Domain.Common;

namespace ERP.Domain.Audit;

/// <summary>
/// Columnas obligatorias de toda auditoría de dominio (append-only, nunca se edita ni se
/// borra). Cada módulo del ERP crea su propia entidad concreta heredando de esta clase y
/// agregando únicamente los campos que su dominio necesita — ver AI-RULES / rediseño de
/// auditoría por dominio. Esta clase NUNCA conoce información de un módulo específico.
/// </summary>
public abstract class AuditRecordBase : ITenantScopedEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid TenantId { get; protected set; }
    public Guid EntityId { get; protected set; }
    public string Action { get; protected set; } = null!;
    public Guid UserId { get; protected set; }

    /// <summary>
    /// Snapshot histórico del nombre visible del usuario al momento del evento — nunca se
    /// actualiza retroactivamente si el usuario cambia su nombre después. Nunca vacío para
    /// un actor autenticado: <see cref="SetCommon"/> aplica <c>"Unknown"</c> como último
    /// recurso si <see cref="AuditActor.UserName"/> llegara vacío (no debería ocurrir si
    /// <c>IAuditContext</c> está bien implementado, pero esta clase no confía ciegamente).
    /// </summary>
    public string UserName { get; protected set; } = null!;
    public DateTime OccurredAtUtc { get; protected set; }
    public string? CorrelationId { get; protected set; }
    public string? RequestId { get; protected set; }
    public AuditSource Source { get; protected set; }
    public string? Reason { get; protected set; }

    protected void SetCommon(AuditActor actor, Guid entityId, string action, string? reason)
    {
        if (actor.TenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(actor));
        if (entityId == Guid.Empty)
            throw new ArgumentException("entityId requerido.", nameof(entityId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("action requerida.", nameof(action));

        TenantId = actor.TenantId;
        EntityId = entityId;
        Action = action.Trim();
        UserId = actor.UserId;
        UserName = string.IsNullOrWhiteSpace(actor.UserName) ? "Unknown" : actor.UserName.Trim();
        OccurredAtUtc = DateTime.UtcNow;
        CorrelationId = actor.CorrelationId;
        RequestId = actor.RequestId;
        Source = actor.Source;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
