using ERP.Domain.Common;
using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// Configuración organizacional jerárquica.
/// Una fila = una clave de configuración para un ámbito específico
/// (empresa, sucursal, establecimiento, punto de emisión o bodega).
/// Restricción única: (tenant_id, company_id, scope, scope_id, key).
/// </summary>
public sealed class OrgSetting : AuditableEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public const int KeyMaxLength = 200;
    public const int ValueMaxLength = 4000;

    public Guid CompanyId { get; private set; }
    public OrgScope Scope { get; private set; }
    public Guid ScopeId { get; private set; }
    public string Key { get; private set; } = null!;
    public string? Value { get; private set; }
    public SettingDataType DataType { get; private set; }

    private OrgSetting() { }

    public static OrgSetting Create(
        Guid tenantId,
        Guid companyId,
        OrgScope scope,
        Guid scopeId,
        string key,
        string? value,
        SettingDataType dataType,
        Guid createdBy)
    {
        var s = new OrgSetting
        {
            TenantId = tenantId,
            CompanyId = companyId,
            Scope = scope,
            ScopeId = scopeId,
            Key = key.Trim().ToLowerInvariant(),
            Value = NormalizeValue(value),
            DataType = dataType,
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void UpdateValue(string? value, Guid updatedBy)
    {
        Value = NormalizeValue(value);
        SetUpdated(updatedBy);
    }

    private static string? NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
