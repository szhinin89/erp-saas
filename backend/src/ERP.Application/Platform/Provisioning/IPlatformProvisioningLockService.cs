namespace ERP.Application.Platform.Provisioning;

/// <summary>
/// Distributed lock preventing concurrent platform first-run provisioning.
/// Backed by a singleton row in <c>platform_provisioning_lock</c> with optimistic update.
/// </summary>
public interface IPlatformProvisioningLockService
{
    /// <summary>
    /// Tries to acquire the provisioning lock for the given instance.
    /// Returns true and the acquired TTL expiry when successful.
    /// Returns false immediately (non-blocking) if another instance holds the lock.
    /// </summary>
    Task<(bool Acquired, DateTime? ExpiresAtUtc)> TryAcquireAsync(
        string instanceId,
        CancellationToken ct = default);

    /// <summary>
    /// Releases the lock, regardless of which instance holds it.
    /// No-op if the lock is already released.
    /// </summary>
    Task ReleaseAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns current lock state for status endpoints.
    /// </summary>
    Task<PlatformLockState> GetStateAsync(CancellationToken ct = default);
}

public sealed record PlatformLockState(
    bool     IsLocked,
    string?  LockedByInstance,
    DateTime? LockedAtUtc,
    DateTime? ExpiresAtUtc,
    bool     IsExpired);
