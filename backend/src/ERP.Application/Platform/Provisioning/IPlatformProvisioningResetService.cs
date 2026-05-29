namespace ERP.Application.Platform.Provisioning;

/// <summary>
/// Development-only: hard-deletes the internal platform owner subscriber + companies
/// and releases the provisioning lock so first-run can be re-executed from scratch.
/// MUST NOT be registered or callable in Production.
/// </summary>
public interface IPlatformProvisioningResetService
{
    Task<PlatformProvisioningResetResult> ResetAsync(CancellationToken ct = default);
}

public sealed record PlatformProvisioningResetResult(
    IReadOnlyList<string> RemovedItems,
    string  SetupToken,
    DateTime ExpiresAtUtc);
