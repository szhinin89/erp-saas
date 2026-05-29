namespace ERP.Domain.Platform.Provisioning;

/// <summary>
/// Distributed provisioning lock — prevents concurrent first-run executions across
/// multiple API instances (Kubernetes pods, Docker replicas, etc.).
///
/// There is exactly ONE row in this table (singleton pattern).
/// Acquire via PlatformProvisioningLockService.TryAcquireAsync().
/// Always released via ReleaseAsync() or expires automatically by ExpiresAtUtc (TTL).
/// </summary>
public sealed class PlatformProvisioningLock
{
    public const string SingletonId = "00000000-0000-0000-0000-000000000001";
    public const int DefaultTtlMinutes = 30;

    public Guid     Id              { get; private set; }
    public bool     IsLocked        { get; private set; }
    public DateTime? LockedAtUtc    { get; private set; }
    /// <summary>Instance identifier (machine name + process id) that holds the lock.</summary>
    public string?  LockedByInstance { get; private set; }
    /// <summary>Lock auto-expires at this time — prevents deadlocks on crashed instances.</summary>
    public DateTime? ExpiresAtUtc   { get; private set; }
    public DateTime UpdatedAt       { get; private set; }

    private PlatformProvisioningLock() { }

    public static PlatformProvisioningLock CreateSingleton() =>
        new()
        {
            Id        = Guid.Parse(SingletonId),
            IsLocked  = false,
            UpdatedAt = DateTime.UtcNow,
        };

    /// <summary>Tries to acquire the lock. Returns false if already locked and not expired.</summary>
    public bool TryAcquire(string instanceId, int ttlMinutes = DefaultTtlMinutes)
    {
        if (IsLocked && ExpiresAtUtc.HasValue && ExpiresAtUtc.Value > DateTime.UtcNow)
            return false; // still held by another instance

        IsLocked         = true;
        LockedAtUtc      = DateTime.UtcNow;
        LockedByInstance = instanceId;
        ExpiresAtUtc     = DateTime.UtcNow.AddMinutes(ttlMinutes);
        UpdatedAt        = DateTime.UtcNow;
        return true;
    }

    public void Release()
    {
        IsLocked         = false;
        LockedAtUtc      = null;
        LockedByInstance = null;
        ExpiresAtUtc     = null;
        UpdatedAt        = DateTime.UtcNow;
    }

    public bool IsExpired => IsLocked && ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= DateTime.UtcNow;
}
