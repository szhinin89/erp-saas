namespace ERP.Application.Platform.Audit;

/// <summary>Proyección de lectura para listados de auditoría del control plane.</summary>
public sealed record PlatformAuditLogListItem(
    Guid Id,
    Guid? ActorUserId,
    string? ActorEmail,
    string Action,
    Guid? TargetSubscriberId,
    string? ResourceType,
    Guid? ResourceId,
    string? Notes,
    DateTime CreatedAtUtc);

public interface IPlatformAuditReader
{
    Task<IReadOnlyList<PlatformAuditLogListItem>> ListRecentAsync(
        int limit,
        Guid? targetSubscriberId = null,
        CancellationToken ct = default);
}
