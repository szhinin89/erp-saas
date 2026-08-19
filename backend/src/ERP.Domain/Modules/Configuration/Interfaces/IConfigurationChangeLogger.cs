using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Interfaces;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: datos de un cambio a registrar. No incluye Id/ChangedAtUtc — los
/// asigna ConfigurationChangeLog.Create.
/// </summary>
public sealed record ConfigurationChangeLogEntry(
    Guid TenantId,
    Guid CompanyId,
    OrgScope Scope,
    Guid ScopeId,
    string? Key,
    string EntityType,
    Guid? EntityId,
    string FieldName,
    string? OldValue,
    string? NewValue,
    ConfigurationChangeValueType ValueType,
    Guid ChangedBy,
    ConfigurationChangeSource Source,
    string? Reason = null,
    bool IsSensitive = false
);

/// <summary>
/// CONFIG-FOUNDATION-P2-01: único punto para registrar un cambio crítico de configuración.
/// La implementación debe encolar el registro en el mismo DbContext/transacción que el cambio
/// que lo origina (Add, sin SaveChangesAsync propio) — así, si SaveChangesAsync falla por
/// cualquier motivo, el cambio de configuración y su log fallan juntos: nunca uno sin el otro.
///
/// No decide "si" debe loguear — esa decisión (RequiresAudit=true, valor realmente cambió) es
/// del llamador (OrgSettingsRepository, los handlers de flags críticos, SriSettings). Este
/// servicio solo persiste lo que se le pide.
/// </summary>
public interface IConfigurationChangeLogger
{
    Task LogAsync(ConfigurationChangeLogEntry entry, CancellationToken cancellationToken = default);
}
