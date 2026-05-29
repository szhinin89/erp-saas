using ERP.Domain.Platform.Provisioning;

namespace ERP.Application.Platform.Provisioning;

/// <summary>
/// Writes immutable audit records for every step of the platform first-run provisioning flow.
/// </summary>
public interface IPlatformProvisioningAuditService
{
    Task RecordAsync(
        ProvisioningEventType eventType,
        bool isSuccess,
        string? instanceId     = null,
        Guid? subscriberId     = null,
        Guid? companyId        = null,
        Guid? operatorUserId   = null,
        string? metadata       = null,
        string? errorMessage   = null,
        CancellationToken ct   = default);

    /// <summary>
    /// Returns all audit events ordered by timestamp ascending.
    /// </summary>
    Task<IReadOnlyList<PlatformProvisioningAuditEvent>> GetHistoryAsync(
        CancellationToken ct = default);
}
