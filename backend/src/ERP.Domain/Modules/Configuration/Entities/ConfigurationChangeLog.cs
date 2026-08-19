using ERP.Domain.Common;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: registro append-only de un cambio crítico de configuración —
/// org_settings marcado RequiresAudit=true, flags IsDefault/IsMain/IsMainBranch, o campos
/// críticos de SriSettings. Nunca se actualiza ni se borra una vez creado; no reemplaza
/// CreatedAt/UpdatedBy de la entidad afectada, es un historial independiente y consultable por
/// key/entidad/scope/rango de fecha.
///
/// Deliberadamente NO tiene relación con UI/permisos — quién puede leer este historial es una
/// decisión de Access metadata en el endpoint que lo exponga, no de esta entidad.
/// </summary>
public sealed class ConfigurationChangeLog : ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Scope conceptual del cambio (Company/Branch/Establishment/...) — para org_settings, el scope real de la key.</summary>
    public OrgScope Scope { get; private set; }
    public Guid ScopeId { get; private set; }

    /// <summary>Key de org_settings cuando EntityType="OrgSetting"; null para flags/entidades.</summary>
    public string? Key { get; private set; }

    /// <summary>"OrgSetting" | "PriceList" | "Branch" | "Establishment" | "EmissionPoint" | "Warehouse" | "SriSettings".</summary>
    public string EntityType { get; private set; } = null!;

    /// <summary>Id de la fila/entidad afectada. Null solo si no aplica (no debería ocurrir en uso normal).</summary>
    public Guid? EntityId { get; private set; }

    /// <summary>"Value" para OrgSetting; "IsDefault"/"IsMain"/"IsMainBranch"/nombre de campo de SriSettings.</summary>
    public string FieldName { get; private set; } = null!;

    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public ConfigurationChangeValueType ValueType { get; private set; }

    public Guid ChangedBy { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public ConfigurationChangeSource Source { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>True si OldValue/NewValue están enmascarados (ValueType Masked/Fingerprint) — nunca contienen el secreto real.</summary>
    public bool IsSensitive { get; private set; }

    private ConfigurationChangeLog() { }

    public static ConfigurationChangeLog Create(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        string? key,
        string entityType,
        Guid? entityId,
        string fieldName,
        string? oldValue,
        string? newValue,
        ConfigurationChangeValueType valueType,
        Guid changedBy,
        ConfigurationChangeSource source,
        string? reason = null,
        bool isSensitive = false
    )
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType es obligatorio.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("FieldName es obligatorio.", nameof(fieldName));

        return new ConfigurationChangeLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Scope = scope,
            ScopeId = scopeId,
            Key = key,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ValueType = valueType,
            ChangedBy = changedBy,
            ChangedAtUtc = DateTime.UtcNow,
            Source = source,
            Reason = reason,
            IsSensitive = isSensitive,
        };
    }
}
