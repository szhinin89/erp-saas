using ERP.Application.Platform.Provisioning;
using ERP.Domain.Platform.Provisioning;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Platform;

public sealed class PlatformProvisioningAuditService : IPlatformProvisioningAuditService
{
    private readonly ErpDbContext _db;
    private readonly ILogger<PlatformProvisioningAuditService> _logger;

    public PlatformProvisioningAuditService(ErpDbContext db, ILogger<PlatformProvisioningAuditService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task RecordAsync(
        ProvisioningEventType eventType,
        bool isSuccess,
        string? instanceId   = null,
        Guid? subscriberId   = null,
        Guid? companyId      = null,
        Guid? operatorUserId = null,
        string? metadata     = null,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var ev = PlatformProvisioningAuditEvent.Record(
            eventType, isSuccess, instanceId,
            subscriberId, companyId, operatorUserId,
            metadata, errorMessage);

        _db.Set<PlatformProvisioningAuditEvent>().Add(ev);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failures must never abort the main flow.
            _logger.LogError(ex, "Failed to write provisioning audit event {EventType}", eventType);
        }
    }

    public async Task<IReadOnlyList<PlatformProvisioningAuditEvent>> GetHistoryAsync(
        CancellationToken ct = default)
    {
        return await _db.Set<PlatformProvisioningAuditEvent>()
            .AsNoTracking()
            .OrderBy(e => e.TimestampUtc)
            .ToListAsync(ct);
    }
}
