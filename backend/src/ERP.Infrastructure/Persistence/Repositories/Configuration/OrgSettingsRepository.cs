using ERP.Domain.Configuration.Definitions;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Configuration;

/// <summary>
/// CONFIG-FOUNDATION-P1-03/P2-01: único punto de escritura real a org_settings (todos los use
/// cases de Companies/Sales pasan por aquí vía UpsertAsync) — por eso el guardrail contra
/// ConfigurationDefinitionCatalog y el registro en ConfigurationChangeLog para keys
/// RequiresAudit=true se centralizan en este único lugar, en vez de en cada handler.
/// </summary>
public sealed class OrgSettingsRepository : IOrgSettingsRepository
{
    private readonly ErpDbContext _db;
    private readonly IConfigurationChangeLogger _changeLogger;

    public OrgSettingsRepository(ErpDbContext db, IConfigurationChangeLogger changeLogger)
    {
        _db = db;
        _changeLogger = changeLogger;
    }

    public Task<OrgSetting?> GetAsync(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        string key,
        CancellationToken ct = default
    ) =>
        _db
            .OrgSettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s =>
                    s.TenantId == tenantId
                    && s.CompanyId == companyId
                    && s.Scope == scope
                    && s.ScopeId == scopeId
                    && s.Key == key.Trim().ToLowerInvariant(),
                ct
            );

    public async Task<IReadOnlyList<OrgSetting>> GetAllForScopeAsync(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        CancellationToken ct = default
    )
    {
        var list = await _db
            .OrgSettings.IgnoreQueryFilters()
            .Where(s =>
                s.TenantId == tenantId
                && s.CompanyId == companyId
                && s.Scope == scope
                && s.ScopeId == scopeId
            )
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task UpsertAsync(OrgSetting setting, CancellationToken ct = default)
    {
        var definition = ValidateAgainstCatalog(setting);

        var existing = await GetAsync(
            setting.TenantId,
            setting.CompanyId,
            setting.Scope,
            setting.ScopeId,
            setting.Key,
            ct
        );

        var oldValue = existing?.Value;
        var newValue = setting.Value;
        var changedBy = setting.UpdatedBy ?? setting.CreatedBy;

        if (existing is null)
            _db.OrgSettings.Add(setting);
        else
            existing.UpdateValue(setting.Value, changedBy);

        // CONFIG-FOUNDATION-P2-01: solo se registra si la definition exige auditoría y el valor
        // realmente cambió — nunca se loguea un no-op (escribir el mismo valor que ya estaba).
        if (definition.RequiresAudit && !string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            await _changeLogger.LogAsync(
                new ConfigurationChangeLogEntry(
                    TenantId: setting.TenantId,
                    CompanyId: setting.CompanyId,
                    Scope: setting.Scope,
                    ScopeId: setting.ScopeId,
                    Key: setting.Key,
                    EntityType: "OrgSetting",
                    EntityId: null,
                    FieldName: "Value",
                    OldValue: oldValue,
                    NewValue: newValue,
                    ValueType: ToChangeValueType(definition.DataType),
                    ChangedBy: changedBy,
                    Source: ConfigurationChangeSource.Api
                ),
                ct
            );
        }
    }

    private static ConfigurationChangeValueType ToChangeValueType(ConfigurationDataType dataType) =>
        dataType switch
        {
            ConfigurationDataType.String => ConfigurationChangeValueType.String,
            ConfigurationDataType.Int => ConfigurationChangeValueType.Int,
            ConfigurationDataType.Decimal => ConfigurationChangeValueType.Decimal,
            ConfigurationDataType.Bool => ConfigurationChangeValueType.Bool,
            ConfigurationDataType.Guid => ConfigurationChangeValueType.Guid,
            ConfigurationDataType.ColorHex => ConfigurationChangeValueType.ColorHex,
            ConfigurationDataType.Json => ConfigurationChangeValueType.Json,
            _ => ConfigurationChangeValueType.String,
        };

    public async Task DeleteAsync(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        string key,
        CancellationToken ct = default
    )
    {
        var existing = await GetAsync(tenantId, companyId, scope, scopeId, key, ct);
        if (existing is not null)
            _db.OrgSettings.Remove(existing);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    /// <summary>
    /// CONFIG-FOUNDATION-P1-03: guardrail de escritura. A partir de esta entrega, org_settings no
    /// acepta keys libres — toda escritura debe existir primero en ConfigurationDefinitionCatalog,
    /// declarar el scope solicitado como permitido, coincidir en DataType, y pasar el Validator de
    /// la definición. Ningún fallback aquí — un valor inválido en escritura se rechaza, nunca se
    /// "corrige" ni se guarda parcialmente (fallback es exclusivo de lectura/resolución).
    /// </summary>
    private static ConfigurationDefinition ValidateAgainstCatalog(OrgSetting setting)
    {
        if (!ConfigurationDefinitionCatalog.TryGet(setting.Key, out var definition))
            throw ConfigurationDefinitionViolationException.UnknownKey(setting.Key);

        if (!definition!.AllowedScopes.Contains(setting.Scope))
            throw ConfigurationDefinitionViolationException.ScopeNotAllowed(setting.Key, setting.Scope);

        if (setting.DataType != definition.PersistedDataType)
            throw ConfigurationDefinitionViolationException.DataTypeMismatch(
                setting.Key,
                setting.DataType,
                definition.PersistedDataType
            );

        if (!definition.IsValidValue(setting.Value))
            throw ConfigurationDefinitionViolationException.InvalidValue(setting.Key, setting.Value);

        return definition;
    }
}
