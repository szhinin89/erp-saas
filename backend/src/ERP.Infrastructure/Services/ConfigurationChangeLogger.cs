using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: única implementación de IConfigurationChangeLogger. Solo hace
/// `Add` sobre el mismo ErpDbContext que el llamador — nunca llama SaveChangesAsync por su
/// cuenta, para que el log viaje en la misma transacción que el cambio que lo origina (si el
/// SaveChangesAsync del llamador falla, el cambio y su log fallan juntos).
/// </summary>
public sealed class ConfigurationChangeLogger : IConfigurationChangeLogger
{
    private readonly ErpDbContext _db;

    public ConfigurationChangeLogger(ErpDbContext db) => _db = db;

    public Task LogAsync(ConfigurationChangeLogEntry entry, CancellationToken cancellationToken = default)
    {
        var log = ConfigurationChangeLog.Create(
            entry.TenantId,
            entry.CompanyId,
            entry.Scope,
            entry.ScopeId,
            entry.Key,
            entry.EntityType,
            entry.EntityId,
            entry.FieldName,
            entry.OldValue,
            entry.NewValue,
            entry.ValueType,
            entry.ChangedBy,
            entry.Source,
            entry.Reason,
            entry.IsSensitive
        );

        _db.ConfigurationChangeLogs.Add(log);
        return Task.CompletedTask;
    }
}
