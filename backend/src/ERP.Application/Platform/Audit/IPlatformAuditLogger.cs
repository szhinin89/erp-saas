namespace ERP.Application.Platform.Audit;

/// <summary>
/// Escribe entradas append-only en <c>platform_audit_logs</c>.
/// La implementación persiste directamente (no usa Outbox — es auditoría síncrona).
/// </summary>
public interface IPlatformAuditLogger
{
    Task LogAsync(
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
        string? notes = null,
        CancellationToken ct = default);
}
