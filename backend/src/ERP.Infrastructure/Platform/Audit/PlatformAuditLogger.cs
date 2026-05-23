using ERP.Application.Platform.Audit;
using ERP.Domain.Platform.Audit.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Platform.Audit;

/// <summary>
/// Implementación síncrona: persiste <see cref="PlatformAuditLog"/> directamente sin Outbox.
/// Falla silenciosamente en prod para no bloquear la operación de negocio (log de error en Serilog).
/// </summary>
public sealed class PlatformAuditLogger : IPlatformAuditLogger
{
    private readonly ErpDbContext _db;
    private readonly ILogger<PlatformAuditLogger> _logger;

    public PlatformAuditLogger(ErpDbContext db, ILogger<PlatformAuditLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
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
        CancellationToken ct = default)
    {
        try
        {
            var entry = PlatformAuditLog.Create(
                action,
                actorUserId,
                actorEmail,
                targetSubscriberId,
                resourceType,
                resourceId,
                oldValueJson,
                newValueJson,
                ipAddress,
                correlationId,
                notes);

            _db.PlatformAuditLogs.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlatformAuditLogger: error persisting audit log for action={Action}", action);
        }
    }
}
