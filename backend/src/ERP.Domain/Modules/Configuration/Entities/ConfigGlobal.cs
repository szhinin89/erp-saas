using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

/// <summary>Configuración de alcance global por tenant (nivel base de la jerarquía).</summary>
public sealed class ConfigGlobal : AuditableEntity
{
    public const int KeyMaxLength = 200;
    public const int DataTypeMaxLength = 32;
    public const int ValueMaxLength = 4000;

    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string DataType { get; private set; } = "string";

    private ConfigGlobal() { }

    public static ConfigGlobal Create(Guid tenantId, string key, string value, string dataType, Guid userId)
    {
        var row = new ConfigGlobal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = NormalizeKey(key),
            Value = NormalizeValue(value),
            DataType = NormalizeDataType(dataType),
        };
        row.SetCreated(userId);
        return row;
    }

    public void UpdateValue(string value, string dataType, Guid userId)
    {
        Value = NormalizeValue(value);
        DataType = NormalizeDataType(dataType);
        SetUpdated(userId);
    }

    private static string NormalizeKey(string key) =>
        (key ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeValue(string value) =>
        (value ?? string.Empty).Trim();

    private static string NormalizeDataType(string dataType) =>
        (dataType ?? "string").Trim().ToLowerInvariant();
}

