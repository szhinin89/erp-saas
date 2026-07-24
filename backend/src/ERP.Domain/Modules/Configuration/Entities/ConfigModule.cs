using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

/// <summary>Configuración de alcance módulo por tenant (prioridad intermedia).</summary>
public sealed class ConfigModule : AuditableEntity
{
    public const int ModuleMaxLength = 120;
    public const int KeyMaxLength = 200;
    public const int DataTypeMaxLength = 32;
    public const int ValueMaxLength = 4000;

    public string Module { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string DataType { get; private set; } = "string";

    private ConfigModule() { }

    public static ConfigModule Create(Guid tenantId, string module, string key, string value, string dataType, Guid userId)
    {
        var row = new ConfigModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Module = NormalizeScope(module),
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

    private static string NormalizeScope(string scope) =>
        (scope ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeKey(string key) =>
        (key ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeValue(string value) =>
        (value ?? string.Empty).Trim();

    private static string NormalizeDataType(string dataType) =>
        (dataType ?? "string").Trim().ToLowerInvariant();
}

