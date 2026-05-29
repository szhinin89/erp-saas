using ERP.Application.Platform.Provisioning;
using ERP.Domain.Platform.Provisioning;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Platform;

public sealed class PlatformProvisioningLockService : IPlatformProvisioningLockService
{
    private readonly ErpDbContext _db;
    private readonly ILogger<PlatformProvisioningLockService> _logger;

    public PlatformProvisioningLockService(ErpDbContext db, ILogger<PlatformProvisioningLockService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<(bool Acquired, DateTime? ExpiresAtUtc)> TryAcquireAsync(
        string instanceId, CancellationToken ct = default)
    {
        // Use serializable isolation to prevent phantom reads on the singleton row.
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var singletonId = Guid.Parse(PlatformProvisioningLock.SingletonId);

            var lockRow = await _db.Set<PlatformProvisioningLock>()
                .FirstOrDefaultAsync(l => l.Id == singletonId, ct);

            if (lockRow is null)
            {
                lockRow = PlatformProvisioningLock.CreateSingleton();
                _db.Set<PlatformProvisioningLock>().Add(lockRow);
            }

            if (!lockRow.TryAcquire(instanceId))
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(
                    "Platform provisioning lock held by {Instance}, expires {Expiry}",
                    lockRow.LockedByInstance, lockRow.ExpiresAtUtc);
                return (false, null);
            }

            if (lockRow.IsExpired)
            {
                _logger.LogWarning(
                    "Recovered expired provisioning lock (was held by {Instance})", lockRow.LockedByInstance);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, lockRow.ExpiresAtUtc);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error acquiring platform provisioning lock");
            throw;
        }
    }

    public async Task ReleaseAsync(CancellationToken ct = default)
    {
        var singletonId = Guid.Parse(PlatformProvisioningLock.SingletonId);
        var lockRow = await _db.Set<PlatformProvisioningLock>()
            .FirstOrDefaultAsync(l => l.Id == singletonId, ct);

        if (lockRow is null) return;

        lockRow.Release();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PlatformLockState> GetStateAsync(CancellationToken ct = default)
    {
        var singletonId = Guid.Parse(PlatformProvisioningLock.SingletonId);
        var lockRow = await _db.Set<PlatformProvisioningLock>()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == singletonId, ct);

        if (lockRow is null)
            return new PlatformLockState(false, null, null, null, false);

        return new PlatformLockState(
            lockRow.IsLocked,
            lockRow.LockedByInstance,
            lockRow.LockedAtUtc,
            lockRow.ExpiresAtUtc,
            lockRow.IsExpired);
    }
}
