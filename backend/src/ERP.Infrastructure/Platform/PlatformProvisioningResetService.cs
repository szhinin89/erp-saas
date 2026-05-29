using ERP.Application.Common.Interfaces;
using ERP.Application.Platform.Provisioning;
using ERP.Domain.Platform.Provisioning;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Platform;

public sealed class PlatformProvisioningResetService : IPlatformProvisioningResetService
{
    private readonly ErpDbContext                       _db;
    private readonly IPlatformProvisioningLockService   _lockService;
    private readonly IPlatformProvisioningAuditService  _auditService;
    private readonly IFirstRunSetupService              _firstRun;
    private readonly ILogger<PlatformProvisioningResetService> _logger;

    public PlatformProvisioningResetService(
        ErpDbContext db,
        IPlatformProvisioningLockService lockService,
        IPlatformProvisioningAuditService auditService,
        IFirstRunSetupService firstRun,
        ILogger<PlatformProvisioningResetService> logger)
    {
        _db           = db;
        _lockService   = lockService;
        _auditService  = auditService;
        _firstRun      = firstRun;
        _logger        = logger;
    }

    public async Task<PlatformProvisioningResetResult> ResetAsync(CancellationToken ct = default)
    {
        var instanceId = $"{Environment.MachineName}:{Environment.ProcessId}:dev-reset";
        var removed    = new List<string>();

        // Hard-delete internal platform companies and their subscribers using direct EF access.
        // This is dev-only; production repositories do not expose hard delete.
        var internalSubscribers = await _db.Subscribers
            .Where(s => s.IsInternalPlatformOwner)
            .ToListAsync(ct);

        foreach (var sub in internalSubscribers)
        {
            var companies = await _db.Companies
                .Where(c => c.SubscriberId == sub.Id)
                .ToListAsync(ct);

            foreach (var co in companies)
            {
                _db.Companies.Remove(co);
                removed.Add($"company:{co.Id}:{co.Ruc}");
            }

            _db.Subscribers.Remove(sub);
            removed.Add($"subscriber:{sub.Id}:{sub.Slug}");
        }

        await _db.SaveChangesAsync(ct);

        // Release lock (no-op if already released)
        await _lockService.ReleaseAsync(ct);

        // Write audit event before reset clears context
        await _auditService.RecordAsync(
            ProvisioningEventType.ProvisioningReset,
            isSuccess:  true,
            instanceId: instanceId,
            metadata:   System.Text.Json.JsonSerializer.Serialize(new { removed }),
            ct: ct);

        // Re-issue a fresh setup token for the next provisioning run
        var tokenResult = await _firstRun.ResetForDevelopmentAsync(ct);

        _logger.LogWarning(
            "PlatformProvisioningResetService: reset executed, removed {Count} items, new setup token issued",
            removed.Count);

        return new PlatformProvisioningResetResult(
            removed,
            tokenResult.SetupToken,
            tokenResult.ExpiresAtUtc);
    }
}
